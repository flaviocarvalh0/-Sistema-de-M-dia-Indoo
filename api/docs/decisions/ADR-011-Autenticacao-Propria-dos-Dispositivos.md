# ADR-011: Autenticação Própria dos Dispositivos (TVs)

## Status

Aceito

## Data da decisão

2026-10-13

## Contexto

As TVs da Mídia Indoor precisam chamar a API sem ninguém por perto: ligam
sozinhas, sincronizam a cada poucos minutos e passam meses sem interação. Não
existe usuário digitando senha nem navegador para guardar cookie.

A plataforma Atlas autentica **pessoas** (Identity, cookie ou JWT de usuário,
RBAC por grupo). Usar esse mecanismo nas TVs exigiria um usuário da plataforma
por TV, com senha guardada no aparelho, permissões de grupo e o risco de um
token de TV abrir o painel. Além disso, o modo Bearer da versão adotada da
plataforma emite a claim `sub` duplicada e é recusado pelo próprio JwtBearer
(ver `docs/platform/Versao-da-Plataforma.md`).

Requisitos:

- Parear a TV com um código curto digitado no controle remoto;
- Revogar o acesso de uma TV pelo painel (desvincular, trocar de aparelho);
- Distinguir "credencial inválida" (voltar ao pareamento) de "tela inativa ou
  parceiro bloqueado" (continuar tocando o que já tem);
- Não alterar o núcleo da plataforma (ADR-003).

## Decisão

As TVs usam um **esquema de autenticação próprio**, `Dispositivo`, registrado
pelo Business:

- **Pareamento:** o painel gera um código de 8 caracteres (alfabeto sem `0`,
  `O`, `1` e `I`), válido por 24 h e de uso único. A TV troca o código por um
  token em `POST /api/v1/dispositivo/autenticar` (anônimo, limitado a 10
  tentativas por IP a cada 5 minutos, resposta igual para qualquer motivo de
  recusa).
- **Token:** JWT assinado com a mesma `Jwt:Key` da plataforma, mas com
  audiência própria (`Indoor.Dispositivos`) e claims da tela: `telaId`,
  `parceiroId`, `dispositivoId` e `versaoCredencial`. Validade de 30 dias
  (`Indoor:Dispositivo:ValidadeTokenDias`), renovado na sincronização
  (`novoToken`) quando falta menos de 1/4 da validade.
- **Registro:** `AddAuthentication().AddJwtBearer("Dispositivo", …)` e a
  política `Dispositivo` (exige a claim `telaId`), sem tocar no
  `MixedAuthSchema` da plataforma. As rotas da TV exigem só essa política; as
  do painel continuam com o RBAC da plataforma.
- **Revogação:** a tela guarda `VersaoCredencial`. Desvincular ou parear outro
  aparelho incrementa a versão; um token com versão antiga recebe **401**.
- **Suspensão:** tela inativa ou parceiro bloqueado recebem **403** — a TV
  mantém a grade e continua tocando.
- `IDispositivoAtual` resolve a tela do token em cada requisição, como
  `IParceiroAtual` faz no painel.

## Justificativa

- O código curto é o único fluxo prático com controle remoto; uso único e
  validade curta limitam o dano de um código visto por terceiros.
- Audiência própria impede que o token da TV seja aceito como token de usuário
  e vice-versa, mesmo com a mesma chave de assinatura.
- `VersaoCredencial` revoga sem lista de bloqueio nem estado no servidor além
  de um inteiro por tela.
- 401 × 403 separados deixam a TV decidir entre "parear de novo" e "continuar
  tocando" sem mensagem de erro para interpretar.

Alternativas rejeitadas:

- **Usuário da plataforma por TV:** senha no aparelho, gestão de usuários
  "técnicos", token capaz de abrir o painel e dependência do modo Bearer com
  defeito.
- **Chave de API fixa por tela:** sem expiração e sem renovação; vazada, vale
  até alguém trocar manualmente.
- **Chave de assinatura separada:** mais um segredo para gerir sem ganho real,
  já que a audiência já isola os tokens.

## Consequências

### Ficou mais fácil

- Parear, trocar e desvincular TVs pelo painel, sem cadastro de usuário.
- Revogar uma TV na hora (próxima sincronização recebe 401).
- Evoluir a plataforma sem conflito: nada do esquema está em `Platform/`.

### Ficou mais difícil

- Trocar a `Jwt:Key` invalida também os tokens das TVs: todas precisam
  parear de novo.
- Uma TV desligada por mais de 30 dias perde o token e precisa de novo código.
- O limite de tentativas por IP depende do IP real: atrás de proxy, é preciso
  preencher `Indoor:ProxiesConfiaveis`.

## Observações de implementação

- `Atlas.Infrastructure/Business/Dispositivo/AutenticacaoDispositivo.cs`
  (esquema, emissão do token, `IDispositivoAtual`).
- `Atlas.Application/Business/Indoor/Telas/Commands/Pareamento/` (código).
- `Atlas.Application/Business/Dispositivo/` (autenticar e sincronizar).
- Contrato para o app: `docs/CONTRATO_DISPOSITIVO.md` (raiz do monorepo).

## Referências

- [ADR-003](ADR-003-Segregacao-Entre-Plataforma-e-Negocio.md) — Segregação entre Plataforma e Negócio
- [ADR-008](ADR-008-Gestao-de-Configuracoes-e-Segredos.md) — Gestão de Configurações e Segredos
- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first

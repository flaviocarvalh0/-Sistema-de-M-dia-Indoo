# ADR-026: Parâmetros Operacionais no Painel

## Status

Aceito

## Data da decisão

2026-10-21

## Contexto

Ajustes do dia a dia da operadora (quem recebe os alertas, depois de quantos
minutos a TV é dada como fora do ar, horário dos alertas, teto do storage,
intervalos das TVs e das coletas) ficavam no appsettings. Para mudar um e-mail
era preciso User Secrets ou variável de ambiente e reiniciar a API — e, no
Docker, recriar o container. Isso misturava ajuste de negócio com segredo.

A Atlas Platform já tem **Parâmetros do sistema** (`platform.system_parameter`),
com tela em Atlas Studio → Parâmetros, tipos (texto, número, booleano, JSON),
histórico e permissão própria (`Parameters.*`).

## Decisão

- Os parâmetros operacionais da Mídia Indoor viram **parâmetros do sistema**, no
  grupo **"Mídia Indoor"**, com chaves `INDOOR.*` (lista em
  `ParametrosIndoor.Todos` e em `docs/IMPLANTACAO.md`).
- **Ordem de leitura:** parâmetro cadastrado → appsettings/variável de ambiente
  (o nome antigo, ex.: `Indoor:Alertas:Emails`) → padrão do código. Os mínimos e
  o teto de 10 GB do storage continuam no código: um valor cadastrado fora da
  faixa é ajustado, não aceito.
- **Primeira subida:** o `SincronizadorParametrosIndoor` cadastra os que faltam
  com o valor da configuração antiga, ou o padrão. Nunca sobrescreve o que já
  existe. Grava em nome do `admin`, porque a tela da plataforma só lista
  parâmetros com autor.
- **Leitura:** uma cópia em memória (`ParametrosIndoor`, singleton), lida antes
  dos outros serviços em segundo plano e relida **a cada minuto**. Mudou no
  painel, vale em até 1 min, sem reiniciar.
- **Fica fora do banco:**
  - **Segredos** (ADR-008): conexão do banco, chave do JWT, senha inicial do
    admin, chaves do R2 e do Supabase.
  - **Endereços da implantação:** `Indoor:UrlPainel` (o CORS é montado na
    subida), `Indoor:UrlPublica`, URLs das APIs de clima e cotações.
  - **Chaves de subida:** `Indoor:Armazenamento:CopiarDoSupabase`.
- O SMTP já era cadastrado pelo painel (Atlas Studio → Servidor SMTP).

## Alternativas consideradas

- **Tela própria de "Configurações da rede":** mais bonita, mas duplicaria o que
  a plataforma já tem (tela, tipos, histórico, permissão).
- **Ler do banco a cada uso (`ISystemParameterProvider`):** assíncrono e com
  escopo; `IConfiguracaoIndoor` é lida em lugares síncronos e singletons. A cópia
  em memória evita mudar todos os chamadores.
- **Manter no appsettings:** exige reiniciar a API e mexer no servidor para um
  ajuste de negócio.

## Consequências

- A operadora ajusta e-mails, horários e o teto pelo painel, sem acesso ao
  servidor.
- Quem tem `Parameters.Update` pode mudar o teto do storage (até 10 GB, nunca
  acima).
- Um valor salvo leva até 1 min para valer.
- As variáveis antigas continuam funcionando só como valor inicial. Depois da
  primeira subida, mudá-las não tem efeito: o valor é o do painel.

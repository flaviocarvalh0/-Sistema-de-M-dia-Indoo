# ADR-014: Posse das Telas pela Operadora

## Status

Aceito

## Data da decisão

2026-10-14

## Contexto

Na primeira versão, o parceiro cadastrava, pareava e excluía as próprias TVs.
Isso não corresponde ao negócio: a operadora da rede é dona do hardware. Ela
compra, instala e pareia as TVs e libera cada uma para um parceiro, que só
decide o conteúdo exibido.

Restrições:

- O parceiro precisa ver as TVs dele e a conexão de cada uma (Online/Offline).
- Ele continua escolhendo a playlist de cada TV.
- O RBAC da plataforma liga cada entidade a um único módulo, e o front decide
  os botões pelas permissões CRUD da entidade (`View`, `Create`, `Update`,
  `Delete`).

## Decisão

- **A entidade `tela` passa para o módulo Rede.** Ela tem item de menu nos dois
  módulos:
  - `/rede/tela`: a operadora tem a gestão completa;
  - `/indoor/tela`: o parceiro só lê.
- **Rotas separadas por papel:**
  - `api/v1/business/rede/telas`, da operadora: cadastro com o parceiro e a
    identificação do aparelho, edição, código de pareamento, desvínculo,
    ativação e exclusão, sem filtro de parceiro;
  - `api/v1/business/parceiro/telas`, do parceiro: listar e ver as próprias
    telas, mais `PUT {id}/playlist`.
- **Permissões:**
  - a Operadora tem `Telas.View/Create/Update/Delete`;
  - o Proprietário de Parceiro tem só `Telas.View`;
  - escolher a playlist exige `Playlists.Update`, porque é gestão de conteúdo
    e não de hardware;
  - as consultas da rede exigem `Telas.View` **e** `Parceiros.View`: o
    parceiro também tem `Telas.View` e, sem a segunda exigência, listaria as
    TVs dos outros parceiros.
- **Trocar uma tela de parceiro** tira a playlist (era do parceiro anterior),
  desvincula a TV e descarta o código pendente (`Tela.TransferirPara`).
- **O parceiro não vê o código de pareamento**, porque parear é trabalho da
  operadora.
- **Identificação do aparelho** (MAC, série ou etiqueta) é um texto
  informativo. O Android 10+ não deixa o app ler o MAC; a identidade da TV
  continua sendo o `dispositivoId` do pareamento (ADR-011).

## Justificativa

- Reflete quem responde pelo hardware e evita TVs "fantasmas" cadastradas
  por parceiros.
- Rotas separadas deixam cada regra de autorização explícita e testável, sem
  `if` de papel dentro dos handlers.
- A transferência entre parceiros não pode deixar uma TV tocando conteúdo de
  quem não é mais dono dela.

Alternativa rejeitada:

- **Uma rota só, decidindo por papel dentro do handler:** mistura as duas
  regras de acesso e torna fácil esquecer um caso.

## Consequências

### Ficou mais fácil

- A operadora controla a rede inteira numa lista, com filtro por parceiro.
- A regra de acesso de cada rota está num atributo `[Authorize]`, coberta por
  teste (`CoberturaAutorizacaoTests.Telas_PoliticasSeguemAPosseDoHardware`).

### Ficou mais difícil

- Um usuário só de leitura da operadora precisaria também de
  `Parceiros.View` para listar as telas.
- O `studio-indoor.sql` precisa manter os dois itens de menu e remover do
  parceiro as permissões de escrita que versões anteriores davam.

## Referências

- [ADR-011](ADR-011-Autenticacao-Propria-dos-Dispositivos.md) — Autenticação própria dos dispositivos

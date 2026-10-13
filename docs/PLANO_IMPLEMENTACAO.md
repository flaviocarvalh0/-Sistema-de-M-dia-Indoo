# Plano de implementação — Mídia Indoor

Monorepo: `api/` (Atlas Platform, .NET 9), `web/` (Angular +
`@atlas/framework`) e `player/` (Flutter, Android TV). Cada etapa fecha com
build verde, testes passando e docs atualizadas.

## Decisões tomadas

| # | Decisão |
|---|---|
| D-01 | Playlist independente e reaproveitável: `Tela.PlaylistId` opcional (1 playlist → N telas) |
| D-02 | Chaves `long` + `Version` (xmin), padrão Atlas, em todas as entidades |
| D-03 | Hash SHA-256 (painel calcula, API grava, TV confere) |
| D-04 | Parceiro cadastrado só pela operadora no MVP (sem autocadastro) |
| D-05 | Storage: Supabase (plano grátis: 1 GB, 50 MB por arquivo), atrás de uma interface própria |
| D-06 | Monorepo `api/`, `web/`, `player/` |

## Etapa 0 — Base limpa da API ✅

- [x] Copiar `playmenu-api` para `api/` e remover todo o Business do PlayMenu
  (código, testes, docs, scripts e migrations `Business_*`).
- [x] Restaurar o snapshot do EF ao estado da última migration da Platform.
- [x] Composição do produto: `IndoorComposicao` (`AddIndoor`/`UseIndoor`) e
  `Infrastructure/Business/DependencyInjection` (políticas de negócio).
- [x] `UserSecretsId` próprios; seção `PlayMenu` e pacote `ClosedXML` removidos.

## Etapa 1 — Modelo de dados ✅

- [x] Domínio: `Parceiro`, `UsuarioParceiro`, `Tela`, `EstadoTela`, `Midia`,
  `Playlist`, `ItemPlaylist`, enums (`DiasSemana` flags, `TipoMidia`,
  `SituacaoTela`, `SituacaoParceiro`, `PapelParceiro`).
- [x] Mapeamento EF (schemas `business` e `indoor`), check constraints e índices.
- [x] `BusinessPermissionKeys` (Parceiros, Telas, Midias, Playlists) e `BusinessRoles`.
- [x] `IParceiroAtual` / `ParceiroAtual` (resolução do inquilino).
- [x] Migration `Business_CriarEstruturaIndoor`, aplicada num PostgreSQL 16;
  catálogo de metadados e triggers de histórico conferidos na subida da API.
- [x] Testes: unitários (domínio, `ParceiroAtual`, políticas) e de integração
  (constraints, cascata, índice filtrado, `DateOnly`).
- [x] [Modelo de dados](../api/docs/MODELO_DADOS.md).

## Etapa 2 — Rede: parceiros (operadora) ✅

- [x] `Rede/Parceiros`: `CriarParceiroCommand` (cria o usuário do responsável
  com senha temporária e troca obrigatória, o parceiro, o vínculo e o e-mail de
  boas-vindas; usuário inativado se o resto falhar), `AtualizarParceiroCommand`
  (com `Version`; o e-mail de acesso não muda), `BloquearParceiroCommand` /
  `DesbloquearParceiroCommand`, `GetParceirosQuery` (busca, filtro por situação,
  telas e armazenamento usado) e `GetParceiroByIdQuery`.
- [x] Validação de CPF/CNPJ (inclusive alfanumérico), telefone com DDD, fuso
  IANA e cota (1 a 1024 MB).
- [x] Endpoint `RedeParceiros` → `/api/v1/business/rede/parceiros`.
- [x] Teste de cobertura de autorização de todo request de negócio.
- [x] `docs/scripts/studio-indoor.sql`: módulos Rede e Mídia Indoor, menus,
  grupos Operadora e Proprietário de Parceiro com as permissões.
- [x] Verificado ponta a ponta na API com PostgreSQL (login em modo Cookie).

> **Pendências da plataforma** (login Bearer e histórico com `char(n)`):
> ver [Versão da Plataforma](../api/docs/platform/Versao-da-Plataforma.md#problemas-conhecidos-da-versão-adotada).

## Etapa 3 — Storage (Supabase) ✅

- [x] `IArmazenamentoMidias` (Application): `PrepararEnvioAsync` (URL assinada
  de envio, `PUT` binário direto do navegador), `GerarUrlLeituraAsync`,
  `ObterAsync` (tamanho e tipo), `AbrirLeituraAsync` (conteúdo, para a API
  conferir) e `RemoverAsync`.
- [x] `TiposArquivoMidia` (MP4, JPEG, PNG detectados pelos bytes),
  `ChavesArmazenamento` (`parceiros/{id}/midias/{guid}.{ext}`, gerada e
  conferida pela API) e `VerificadorArquivoMidia` (SHA-256, tamanho e tipo
  numa leitura só, parando acima de 50 MB).
- [x] `ArmazenamentoSupabase` (Infrastructure) sobre a Storage API, com a
  chave `service_role` em segredo; bucket privado `midias` criado na subida
  (50 MB, MP4/JPEG/PNG). Sem configuração a API sobe e o envio responde 503.
- [x] Falhas do storage viram 503 com mensagem clara (`IndoorExceptionHandler`).
- [x] Testes do contrato HTTP (handler falso) e teste de ponta a ponta
  contra uma Storage API (pula sem `INDOOR_SUPABASE_*`).
- [x] Correção: `hash` e `codigo_pareamento` passam a `varchar` (migration
  `Business_HashECodigoComoVarchar`) — o histórico da plataforma não lê
  `char(n)` e a API não subia pela segunda vez no mesmo banco.

## Etapa 4 — CRUD do painel ✅

- [x] **Mídias** (`/api/v1/business/parceiro/midias`): `envio` (tipo, tamanho e
  cota → URL assinada), `POST /` (a API lê o arquivo, confere tipo, tamanho e
  SHA-256 e grava o hash calculado; arquivo recusado é apagado do storage),
  edição (título, duração, período), ativar/inativar, excluir (só fora de
  playlists; apaga o arquivo), lista, detalhe com URL de visualização e uso
  da cota (`armazenamento`).
- [x] **Playlists** (`/parceiro/playlists`): CRUD, `PUT /{id}/itens` (grade
  inteira na ordem, só mídias do parceiro, com versão), ativar/inativar,
  excluir (só fora de telas), detalhe com itens e duração do ciclo.
- [x] **Telas** (`/parceiro/telas`): CRUD (playlist do mesmo parceiro),
  `codigo-pareamento` (8 caracteres legíveis, 24 h), `desvincular`,
  ativar/inativar, excluir; situação calculada no SQL.
- [x] Isolamento: registro de outro parceiro dá 404; usuário sem parceiro, 403.
- [x] Testes: unitários (regras e isolamento), integração no PostgreSQL
  (tradução das consultas, concorrência por `xmin` nos itens) e roteiro de
  ponta a ponta por HTTP. [Como testar](COMO_TESTAR.md) e `Indoor.http`.

## Etapa 5 — Dispositivo: autenticação e sincronização ✅

- [x] Esquema JWT `Dispositivo` próprio (mesma chave da plataforma, audiência
  `Indoor.Dispositivos`), registrado sem alterar o `MixedAuthSchema`: as rotas
  da TV exigem a política `Dispositivo`; o token da TV não abre o painel e o
  login de usuário não abre as rotas da TV.
- [x] `POST /api/v1/dispositivo/autenticar` (anônimo, 10 tentativas por IP a
  cada 5 min): código + `dispositivoId` → pareia, consome o código e devolve
  o token (30 dias). Mesma resposta para qualquer motivo de recusa.
- [x] `IDispositivoAtual`: 401 para credencial revogada (TV desvinculada ou
  outra pareada — `VersaoCredencial`), 403 para tela inativa ou parceiro bloqueado.
- [x] `GET /api/v1/dispositivo/sincronizacao`: grade com tudo o que ainda
  pode tocar (dias, período e `exibirHoje` no fuso do parceiro), URLs de
  download assinadas numa chamada só ao storage, `versaoGrade` (sem itens
  quando nada mudou), renovação do token e registro em `EstadoTela`.
- [x] Situação Online/Offline calculada pela última sincronização (sem job).
- [x] [Contrato do app das TVs](CONTRATO_DISPOSITIVO.md).
- [x] Testes: unitários (pareamento, credencial, filtros, versão, fuso,
  renovação, token real) e roteiro de ponta a ponta por HTTP.

## Etapa 6 — Painel `web/`

- [x] Cópia do `playmenu-web` sem as features do PlayMenu; ficaram `shared/`
  (paginação, formulário, telefone, documento), notas de versão, gerador de
  versão e publicação no IIS. Marca própria (`atlas-brand-indoor.css`).
- [x] Features `rede` (parceiros), `telas`, `midias` e `playlists` no padrão
  service + configs + list-page + `FormModalService`, com histórico na edição.
- [x] Envio de mídia em página própria: SHA-256 com `crypto.subtle`, duração
  lida do vídeo, PUT direto na URL assinada (sem passar pela API) com
  progresso, e conferência de formato, tamanho e cota antes de subir.
- [x] Grade da playlist: adicionar mídias, reordenar arrastando (CDK), dias da
  semana por item com atalhos, salvar a grade inteira com a versão.
  "Clonar" na grid duplica a playlist com a grade.
- [x] Tela: código de pareamento em destaque com contagem regressiva, conexão
  (Aguardando pareamento/Online/Offline) atualizada a cada minuto, desvincular,
  "parear outra TV". "Clonar" cria outra tela com o mesmo local e playlist.
- [x] `ItemPlaylistDto.DiasSemana` passou a sair como número (bits), como no
  contrato da TV.
- [x] Roteiro de navegador (Playwright) de ponta a ponta: login do dono, envio
  de vídeo e imagem, grade com arrastar e dias, tela com código, duplicações e
  a lista de parceiros da operadora.

## Etapa 7 — Player `player/` (Flutter, offline-first)

- [x] Projeto Flutter para Android TV: launcher leanback com banner, paisagem,
  tela cheia imersiva e tela sempre acesa (`wakelock_plus`).
- [x] Pareamento por código (aceita minúsculas e hífen); token, id do
  aparelho e endereço do servidor em `flutter_secure_storage`.
- [x] Sincronizador: `ApiDispositivo` (dio, erros tipados) → `BaixadorMidia`
  (`.part`, SHA-256 em blocos, renomeia; 3 tentativas com espera crescente)
  → troca atômica da grade no `sqflite` → limpeza dos arquivos que saíram.
  Arquivos nomeados pelo hash (mídia repetida não baixa de novo).
- [x] 401 apaga o token e continua tocando com aviso "pressione OK para
  parear"; 403/429/5xx/sem rede continuam tocando; URL vencida ou hash
  errado mantêm a grade antiga e tentam de novo em 1 minuto; `novoToken`
  renovado.
- [x] Reprodução só de arquivos locais (`VideoPlayerController.file`,
  `Image.file`), loop pela `ordem`, filtro do dia recalculado a cada item no
  fuso do parceiro (dias da semana e período), arquivo que falha é pulado,
  tela de espera com o nome da tela.
- [x] Painel de informações no botão OK do controle (conexão, última
  sincronização, itens de hoje, servidor, versão, "Parear de novo").
- [x] Abertura automática ao ligar (`BOOT_COMPLETED`; Android 10+ pede
  "Sobrepor a outros apps").
- [x] Testes: filtro do dia, download (hash, tentativas, URL vencida,
  limpeza) e sincronizador com API/storage falsos em `HttpServer` e SQLite
  real; teste de ponta a ponta contra a API (pareia, sincroniza e baixa).

## Etapa 8 — Fechamento

- [ ] Catálogo de funcionalidades, ADRs (esquema Dispositivo, storage externo)
  e guia de implantação.

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

## Etapa 4 — CRUD do painel

- [ ] **Mídias:** `PrepararEnvioMidiaCommand` (valida tipo, tamanho e cota →
  URL de envio), `CriarMidiaCommand` (confere no storage que o objeto existe e
  tem o tamanho informado), `AtualizarMidiaCommand` (título, duração, período),
  ativar/inativar, excluir (só fora de playlists; remove do storage), queries.
- [ ] **Playlists:** criar/atualizar (com `Version`), `DefinirItensPlaylistCommand`
  (lista inteira na ordem; mídias do mesmo parceiro), ativar/inativar, queries
  (itens com título e tipo da mídia).
- [ ] **Telas:** criar/atualizar (playlist do mesmo parceiro),
  `GerarCodigoPareamentoCommand` (8 caracteres sem ambíguos, 24 h),
  `DesvincularTelaCommand`, ativar/inativar, queries com situação calculada.
- [ ] Endpoints `ParceiroMidias`, `ParceiroPlaylists`, `ParceiroTelas` →
  `/api/v1/business/parceiro/...`.
- [ ] Testes de integração de isolamento A × B para as três entidades.

## Etapa 5 — Dispositivo: autenticação e sincronização

- [ ] Esquema JWT `Dispositivo` (audience própria, claims `tela_id`,
  `parceiro_id`, `versao_credencial`), roteado no `MixedAuthSchema` por
  `/api/v1/dispositivo/*`. O JwtBearer da plataforma exige `SessionId` de
  usuário e recusaria o token da TV.
- [ ] `AutenticarDispositivoCommand` (anônimo, limite de taxa por IP): código +
  `dispositivoId` → pareia, consome o código e devolve o token.
- [ ] `IDispositivoAtual`: confere `versao_credencial`, tela ativa e parceiro não bloqueado.
- [ ] `GetSincronizacaoQuery`: grade da playlist da tela, filtrada por
  vigência e dia da semana **no fuso do parceiro**, ordenada; URL assinada por
  mídia; `versaoGrade` (hash) como ETag com resposta 304; renova o token
  perto de expirar; grava `EstadoTela`.
- [ ] Endpoint `Dispositivo` → `POST /api/v1/dispositivo/autenticar`,
  `GET /api/v1/dispositivo/sincronizacao`.
- [ ] Testes: código de uso único/expirado, token antigo após desvincular,
  filtro por data/dia/fuso, ETag.

## Etapa 6 — Painel `web/`

- [ ] Copiar `playmenu-web`, remover as features do PlayMenu, manter `shared/`.
- [ ] Features `rede/parceiros`, `telas`, `midias`, `playlists` no padrão
  service + configs + list-page + `FormModalService`.
- [ ] `UploadMidiaService` (SHA-256 com `crypto.subtle`, duração do vídeo, PUT
  direto na URL assinada com progresso).
- [ ] Playlist: seleção de mídias, reordenação por arrastar, dias da semana.
- [ ] Tela: gerar/exibir código de pareamento, situação Online/Offline.

## Etapa 7 — Player `player/` (Flutter, offline-first)

- [ ] Pareamento (`flutter_secure_storage` para o token).
- [ ] Sincronizador: `SyncApi` → `SyncDiff` → `MediaDownloader` (`dio`, `.part`
  e renomeia) → `FileHasher` (SHA-256 em stream) → `MediaDao` (`sqflite`).
- [ ] Reprodução só de arquivos locais (`VideoPlayerController.file`,
  `Image.file`), loop infinito, refiltro local diário por dia/vigência.
- [ ] Boot automático, wakelock e modo imersivo.

## Etapa 8 — Fechamento

- [ ] Catálogo de funcionalidades, ADRs (esquema Dispositivo, storage externo)
  e guia de implantação.

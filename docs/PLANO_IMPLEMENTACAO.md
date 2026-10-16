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

- [x] [Catálogo de funcionalidades](FUNCIONALIDADES.md) do produto.
- [x] ADRs: [011 — autenticação própria dos dispositivos](../api/docs/decisions/ADR-011-Autenticacao-Propria-dos-Dispositivos.md),
  [012 — armazenamento externo das mídias](../api/docs/decisions/ADR-012-Armazenamento-Externo-de-Midias.md) e
  [013 — player offline-first](../api/docs/decisions/ADR-013-Player-Offline-First.md).
- [x] [Guia de implantação](IMPLANTACAO.md): API, banco, Supabase, painel,
  SMTP, APK das TVs e checklist de produção.
- [x] CORS da origem do painel em produção (`Indoor:UrlPainel`), sem alterar
  a lista fixa da plataforma.

## Rodada 2 — Ajustes do MVP

Decisões: repetir mídia na playlist é permitido (seletor só marca "já
adicionado"); a fila é montada na TV; notícias da Agência Brasil (CC BY);
"1 notícia a cada X anúncios" fica na playlist; o grupo continua "Operadora".

### Entrega 1 — Posse das telas (RBAC) ✅

- [x] A operadora é dona do hardware ([ADR-014](../api/docs/decisions/ADR-014-Posse-das-Telas-pela-Operadora.md)):
  rotas `rede/telas` (cadastro com parceiro, identificação do aparelho,
  pareamento, desvínculo, ativação, exclusão) e `parceiro/telas` só leitura
  com `PUT {id}/playlist`.
- [x] Trocar a tela de parceiro tira a playlist, desvincula a TV e descarta o
  código; o parceiro não vê o código de pareamento.
- [x] Migration `Business_IdentificacaoAparelhoTela`; `studio-indoor.sql` com
  `tela` no módulo Rede, menu nos dois módulos e permissões novas.
- [x] Painel: `Rede → Telas` (operadora) e `Mídia Indoor → Telas` somente
  leitura com "Trocar playlist".
- [x] Testes: unitários das duas visões e das políticas, integração, roteiro
  HTTP de ponta a ponta (operadora, dois parceiros, TV e transferência) e
  roteiro no navegador.

### Entrega 2 — Player: troca de grade sem travar ✅

- [x] `ControladorReproducao` (loop sem tela, testável): cada item ganha uma
  geração; fim repetido, timer de segurança atrasado e falha de imagem já
  trocada são ignorados. Antes, dois "fins" do mesmo vídeo abriam o próximo
  vídeo mais de uma vez e podiam pular itens.
- [x] Grade nova entra no fim do item atual, sem interromper; na espera,
  começa na hora.
- [x] Ouvinte do vídeo removido antes do `dispose`.
- [x] Sincronização a cada 60 s (API e app); Offline depois de 5 min sem
  sincronizar.
- [x] "Atualizar TV agora" ficou fora: sem push, o aviso só chegaria na
  próxima sincronização, que agora já é em até 60 s.
- [x] Testes do loop com relógio simulado (13), conferidos removendo a
  proteção: o teste de vídeo depois de vídeo falha sem ela.

### Entrega 1B — Playlist da operadora e plano do anunciante ✅

- [x] Playlist da rede ([ADR-015](../api/docs/decisions/ADR-015-Playlists-da-Operadora-e-Plano-do-Anunciante.md)):
  rotas `rede/playlists` só da operadora; nome único na rede; item novo só
  com mídia ativa. A operadora lê as mídias de todos em `rede/midias`.
- [x] Tela com host (fuso) e playlist escolhidos pela operadora; trocar não
  desvincula a TV. A rota `PUT parceiro/telas/{id}/playlist` saiu.
- [x] Plano: `Parceiro.LimiteMidiasAtivas` (padrão 1). Mídia nova nasce
  inativa com o plano completo; ativar além dá 400; reduzir não inativa nada.
- [x] Substituir arquivo (`PUT parceiro/midias/{id}/arquivo`): mesmas
  conferências do envio, cota descontando o arquivo antigo, hash novo.
- [x] "Onde meus anúncios passam": telas onde o parceiro tem mídia ativa.
- [x] TV: host bloqueado não afeta; mídias de anunciante bloqueado saem da
  grade; 403 só para tela inativa.
- [x] Migration `Business_PlaylistDaOperadoraELimiteMidias` (renomeia nomes
  repetidos antes do índice único); `studio-indoor.sql` com Playlists no
  módulo Rede e menu "Onde meus anúncios passam".
- [x] Painel: Rede → Playlists com seletor de mídias da rede, plano e
  "Substituir arquivo" em Mídias, campo de plano no parceiro.
- [x] Testes: unitários, integração, roteiro HTTP de ponta a ponta e roteiro
  no navegador (operadora e anunciante).

### Entrega 3 — Seletor da grade ✅

- [x] Badge "Já adicionado (N×)" no seletor de mídias da grade (repetição
  permitida), entregue junto da 1B.

### Entrega 4 — Notícias, randomização, relógio e clima ✅

- [x] Composição da fila na TV ([ADR-016](../api/docs/decisions/ADR-016-Composicao-da-Fila-na-TV-Noticias-e-Clima.md)):
  na playlist, notícia a cada N anúncios, tempo da notícia, embaralhar,
  relógio e clima; 📌 posição fixa por item da grade.
- [x] Fontes de notícias (Rede → Fontes de notícias), com a Agência Brasil
  cadastrada pela migration; coletor RSS/Atom de hora em hora, imagens no
  storage com hash, 30 por fonte, 48 h de validade; "Coletar agora".
- [x] Clima pelo Open-Meteo (geocodificação da cidade do host, a cada 30 min).
- [x] Sincronização com `composicao`, `posicaoFixa`, `noticias` (versão
  própria) e `clima`; contrato atualizado (regras 10 a 12).
- [x] Player: `ComposicaoFila`, tela de notícia, relógio e clima sobrepostos,
  banco local v2 com migração.
- [x] Testes: unitários da API (coletores com feed e Open-Meteo falsos,
  sincronização, cadastro), roteiro HTTP de ponta a ponta com coleta
  automática, roteiro no navegador, testes do player (fila, loop com
  notícias, sincronização, telas) e o player contra a API de verdade.


### QR code nos anúncios ✅

- [x] QR code opcional por mídia ([ADR-017](../api/docs/decisions/ADR-017-QR-Code-nos-Anuncios.md)):
  Link, WhatsApp, Instagram, Telefone e Pix (copia e cola do Banco Central),
  com legenda, canto e tamanho; o formulário pede só os campos do tipo.
- [x] Conteúdo montado e validado na API; vai no item da sincronização
  (`qrCode`, regra 14 do contrato) e muda a `versaoGrade`.
- [x] Painel: aba "QR code" na edição, coluna na lista e prévia ("Ver QR code").
- [x] Player: QR gerado na TV por cima da mídia (`qr_flutter`), banco local v3.
- [x] Testes: montagem e validação de cada tipo (Pix conferido com o exemplo
  do manual do Banco Central), restrições no Postgres, sincronização, banco
  local e tela do player em vários tamanhos, roteiro no navegador.

### Entrega 5 — Diretório da TV (fase 1) ✅

- [x] Dupla autorização ([ADR-018](../api/docs/decisions/ADR-018-Diretorio-de-Anunciantes-da-TV.md)):
  a operadora liga o diretório na tela; cada parceiro aceita aparecer no
  perfil público (padrão não; só o dono; data do aceite gravada).
- [x] Página pública `/d/{codigo}` servida pela API (HTML leve, sem
  framework: o pacote inicial do painel tem ~440 KB), código aleatório por
  tela, "Trocar endereço", cache de 60 s, limite por IP, CSP com nonce; o
  mesmo conteúdo em JSON.
- [x] Lista: estabelecimento primeiro, depois os anunciantes da grade de
  hoje; só os contatos cadastrados no perfil público.
- [x] Painel: diretório na edição da tela com QR e endereço; perfil público
  no topo de Mídias; coluna "Diretório das TVs" em Parceiros.
- [x] Player: `diretorio` na sincronização (regra 15), QR fixo pequeno que
  some quando o anúncio tem QR próprio.
- [x] `Indoor:UrlPublica` e a API com HTTPS na internet (IMPLANTACAO.md).
- [ ] Fase 2: contagem de acessos e cliques por anunciante, logo no cartão.

### Carrossel com cursor na fila da TV ✅

- [x] "Tamanho da volta" na playlist (`DuracaoMaximaVoltaSegundos`, 0 = todos
  em toda volta) e na composição da sincronização
  ([ADR-019](../api/docs/decisions/ADR-019-Carrossel-com-Cursor.md)).
- [x] Player: `CarrosselAnuncios` — fixos em toda volta, os outros puxados de
  uma lista circular a partir do cursor, reembaralhada no fim, sem repetir na
  volta nem emendar o mesmo anúncio; grade nova reconciliada sem perder o lugar.
- [x] A volta em andamento não é remontada a cada sincronização (gastaria o cursor).
- [x] Painel: campo na playlist e "todos passam a cada N voltas" na grade.

### Aprovação da troca de arquivo e alerta de TV fora do ar ✅

- [x] Troca de arquivo de mídia em grade fica pendente até a operadora aprovar
  ou recusar com motivo, com e-mail para os dois lados; fora de grade troca na
  hora ([ADR-020](../api/docs/decisions/ADR-020-Aprovacao-de-Troca-e-Alerta-de-TV-Offline.md)).
- [x] Painel: cartão "Trocas de arquivo para aprovar" em Rede → Playlists com
  comparação lado a lado; situação da troca na lista de mídias do parceiro.
- [x] `MonitorTelas`: e-mail-resumo das TVs que caíram e voltaram, com
  tolerância e horário configuráveis (`Indoor:Alertas:*`) no fuso do
  estabelecimento.

### Atualização automática do app das TVs ✅

- [x] `VersaoApp` com distribuição Suspensa / Teste / Todas e `Tela.TvDeTeste`
  ([ADR-021](../api/docs/decisions/ADR-021-Atualizacao-Automatica-do-App-das-TVs.md)).
- [x] Sincronização recebe `versaoCodigoApp` e devolve `atualizacao`.
- [x] Player: `AtualizadorApp` (baixa com hash, espera 1 h após falha) e
  instalador nativo (`PackageInstaller`, device owner sem confirmação).
- [x] Painel: Rede → Telas → Versões do app; coluna App e "TV de teste".
- [ ] Validar numa TV real: instalação com e sem device owner e reabertura
  após atualizar (o código nativo não é compilado no ambiente de CI).


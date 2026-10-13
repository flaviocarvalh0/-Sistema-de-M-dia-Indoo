# Modelo de dados — Mídia Indoor

Migration: `Business_CriarEstruturaIndoor`. Todas as entidades seguem o padrão
Atlas: chave `long`, concorrência otimista pela coluna `xmin` (propriedade
`Version`), auditoria (`created`, `created_by`, `last_modified`,
`last_modified_by`) e nomes em snake_case.

```text
business.parceiro ──< business.usuario_parceiro >── platform.user
      │
      ├──< indoor.midia ──────────────┐
      │                               │
      ├──< indoor.playlist ──< indoor.item_playlist
      │          ▲
      └──< indoor.tela ── (playlist_id, opcional)
                 │
                 └── indoor.estado_tela (1:1)
```

## Diferenças em relação à spec original

| Spec | Implementado | Motivo |
|---|---|---|
| `Guid Id` | `long Id` + `Version` (xmin) | Padrão Atlas (histórico, RBAC e listagens dependem dele) |
| `Tenant` | `Parceiro` + `UsuarioParceiro` | O Atlas não tem `TenantId`; o inquilino vem do vínculo do usuário (`IParceiroAtual`) |
| `Screen` / `Media` / `PlaylistItem` | `Tela` / `Midia` / `ItemPlaylist` | Negócio em português (ADR-004) |
| `Playlist.ScreenId` | `Tela.PlaylistId` (opcional) | Uma playlist roda em N telas |
| `MacAddress` ou `PairingCode` | `CodigoPareamento` (uso único, 24 h) + `DispositivoId` | MAC é falsificável e o Android 10+ o esconde |
| `LastSync`, `Status` | `EstadoTela.UltimaSincronizacao`; situação calculada | Não gravar uma versão de histórico da tela a cada sync |
| `FileUrl` | `ChaveArmazenamento` | A URL (assinada) é gerada na hora pelo storage |
| `FileHash` MD5 | `Hash` SHA-256 | Mais seguro; já é o algoritmo das imagens da plataforma |
| `StartDate`/`EndDate` `DateTime?` | `InicioExibicao`/`FimExibicao` `DateOnly?` | A validade é por dia, no fuso do parceiro |
| `DaysOfWeek` `"1,2,3"` | `DiasSemana` flags (`int`) | Tipado, validável e filtrável no SQL |

## Tabelas

### `business.parceiro` — o inquilino

| Coluna | Tipo | Regras |
|---|---|---|
| `nome` | varchar(150) | obrigatório |
| `nome_responsavel` | varchar(150) | |
| `email` | varchar(254) | obrigatório |
| `telefone` | varchar(11) | só dígitos com DDD (10 ou 11) |
| `documento` | varchar(14) | CPF (11 dígitos) ou CNPJ (alfanumérico) |
| `cidade` | varchar(100) | |
| `fuso_horario` | varchar(64) | IANA, padrão `America/Sao_Paulo` |
| `cota_armazenamento_mb` | int | padrão 200; 1 a 1.048.576 |
| `situacao` | int | 1 Ativo, 2 Bloqueado (exige `motivo_bloqueio`) |
| `motivo_bloqueio` | varchar(500) | |
| `observacao_interna` | text | só a operadora vê |

Histórico ativo. Permissões `Parceiros.*` (operadora).

### `business.usuario_parceiro`

Vínculo usuário → parceiro. `usuario_id` é único (um usuário, um parceiro).
`papel`: 1 Proprietário, 2 Equipe. Sem tela própria.

### `indoor.midia`

| Coluna | Tipo | Regras |
|---|---|---|
| `parceiro_id` | bigint | FK `parceiro` (restrict) |
| `titulo` | varchar(150) | obrigatório |
| `tipo` | int | 1 Vídeo, 2 Imagem |
| `chave_armazenamento` | varchar(300) | única; caminho no bucket |
| `tipo_conteudo` | varchar(50) | `video/mp4`, `image/jpeg`, `image/png` |
| `tamanho_bytes` | bigint | 1 a 50 MB (limite do Supabase grátis) |
| `hash` | char(64) | SHA-256 hex minúsculo |
| `duracao_segundos` | int | padrão 15; 1 a 3600 |
| `inicio_exibicao` / `fim_exibicao` | date | fim ≥ início; nulos = sem limite |
| `ativo` | bool | inativa sai da grade |

Índices: `(parceiro_id, ativo)`. Histórico ativo. Permissões `Midias.*`.

### `indoor.playlist` e `indoor.item_playlist`

`playlist`: `nome` único por parceiro, `descricao`, `ativo`. Histórico ativo.
Permissões `Playlists.*`.

`item_playlist`: `playlist_id` (cascade), `midia_id` (restrict — mídia em uso
não pode ser apagada), `ordem` (≥ 0), `dias_semana` (flags 1 a 127: domingo =
1, segunda = 2, … sábado = 64). A mesma mídia pode aparecer mais de uma vez.
Os itens são gravados sempre juntos com `Playlist.DefinirItens`, em que a
posição na lista vira a ordem.

### `indoor.tela`

| Coluna | Tipo | Regras |
|---|---|---|
| `parceiro_id` | bigint | FK `parceiro` |
| `nome` | varchar(100) | único por parceiro |
| `localizacao` | varchar(150) | |
| `playlist_id` | bigint | opcional; FK `playlist` (restrict) |
| `ativo` | bool | |
| `codigo_pareamento` | char(8) | `[A-Z0-9]{8}`, único entre os não nulos |
| `codigo_pareamento_expira_em` | timestamptz | obrigatório com o código |
| `dispositivo_id` | varchar(100) | nulo = sem TV |
| `pareada_em` | timestamptz | presente se e só se há dispositivo |
| `versao_credencial` | int | vai no token da TV; incrementa ao parear/desvincular |

Histórico ativo. Permissões `Telas.*`.

### `indoor.estado_tela`

Telemetria 1:1 com a tela (chave = `tela_id`, cascade): `ultima_sincronizacao`,
`versao_app`, `versao_grade` (ETag da última grade entregue), `itens_prontos`.
Sem histórico. A situação exibida no painel (Aguardando pareamento, Online,
Offline) é calculada por `Tela.Situacao(agora, tolerância)`.

## Regras de isolamento

- Nenhum handler recebe `parceiroId` do cliente: o painel usa
  `IParceiroAtual` (vínculo do usuário logado) e a TV usa as claims do token
  do dispositivo.
- Registro de outro parceiro responde 404, nunca 403 (não revela que existe).
- `Tela.PlaylistId` e `ItemPlaylist.MidiaId` precisam pertencer ao mesmo
  parceiro — conferido nos handlers (o banco não cruza o parceiro nas FKs).

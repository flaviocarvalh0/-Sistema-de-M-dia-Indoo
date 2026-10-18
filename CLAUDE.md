# Instruções do projeto — Sistema de Mídia Indoor

## Commits

- **Nunca** incluir o Claude como coautor ou autor: sem `Co-Authored-By`, sem
  linha `Claude-Session` e sem qualquer menção ao Claude ou a IA na mensagem.
  O autor do commit é sempre o dono do repositório:
  `git config user.name "Flávio Carvalho"` e
  `git config user.email "flavio.carvalho.pcg@gmail.com"` neste repositório
  (confira antes do primeiro commit de cada sessão).
- Mensagens em português, no padrão Conventional Commits
  (`feat(api): ...`, `fix(api): ...`, `docs: ...`).

## Estrutura

- `api/` — backend .NET 9 sobre a Atlas Platform (ver `api/README.md` e
  `api/docs/platform/Versao-da-Plataforma.md`).
- `web/` — painel Angular com `@atlas/framework` (etapa 6).
- `player/` — app Flutter das TVs (etapa 7).
- Plano e decisões: `docs/PLANO_IMPLEMENTACAO.md`. Como testar: `docs/COMO_TESTAR.md`.

## Convenções da API

- Código de negócio em português (ADR-004), em `Business/` de cada camada;
  nada do produto dentro de `Platform/`.
- Vocabulário (ADR-022): **parceiro** é o local onde a TV fica (sem acesso ao
  sistema); **cliente** é o anunciante (tem login, mídias e plano). Parceiro
  que também anuncia é cadastrado como cliente e ligado em `Parceiro.ClienteId`.
- Todo handler do painel do anunciante resolve o cliente por `IClienteAtual`
  e filtra por `ClienteId` (registro de outro cliente = 404).
- Colunas de texto de tamanho fixo usam `varchar(n)` + check constraint, nunca
  `IsFixedLength()` (o histórico da plataforma não lê `char(n)`).
- Segredos só em User Secrets ou variáveis de ambiente (ADR-008).

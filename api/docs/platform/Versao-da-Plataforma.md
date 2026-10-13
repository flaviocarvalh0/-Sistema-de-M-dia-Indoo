# Versão da Plataforma

## Plataforma adotada

- Plataforma: Atlas Platform
- Versão: `v0.4.0-alpha.1`
- Repositório de origem: `atlas-platform` (remote `platform`)
- Data de adoção: `09/10/2026`
- Commit de origem: `5ba50f43144a64f5af309a49038259be14b37a28`

## Estado da adoção

O produto **Mídia Indoor** nasce do `playmenu-api` (produto irmão, também
sobre a tag `v0.4.0-alpha.1`), copiado para a pasta `api/` do monorepo
`-Sistema-de-M-dia-Indoo` e limpo de todo o Business do PlayMenu. Por estar
numa subpasta de outro repositório, **não** preserva a ancestralidade Git da
plataforma: uma tag nova da Atlas Platform entra por comparação
(`git diff <tag-atual> <tag-nova>` no repositório da plataforma) aplicada
nesta pasta, conferindo os pontos de composição listados abaixo.

Commits da plataforma posteriores à tag **não** foram incorporados. Na data da
adoção, a `main` da plataforma tinha dois a mais:

| Commit | Descrição |
|---|---|
| `ba81daf` | fix(auth): remove claim sub duplicada do token jwt |
| `7936dd0` | merge: integra correcao da autenticacao bearer |

Eles entram quando houver uma tag oficial que os contenha.

## Identidade técnica mantida

Solução, projetos, assemblies e namespaces herdados mantêm o prefixo `Atlas`. A
identidade do produto vem do nome do repositório, do `README.md`, da
documentação em `docs/` e dos módulos da área `Business`.

## Alterações locais na plataforma

Uma, de configuração, necessária para isolar o produto:

- `UserSecretsId` de `Atlas.Web` e de `Atlas.Infrastructure.IntegrationTests`
  trocados por identificadores próprios da Mídia Indoor (diferentes também dos
  do PlayMenu) — na mesma máquina, a API leria os segredos (inclusive a
  connection string) do outro produto.

Nenhuma alteração no código do núcleo `Platform`.

### Pontos de composição do produto em arquivos da plataforma

Não mudam comportamento da plataforma, mas aparecem como conflito em potencial
no merge de uma tag nova:

| Arquivo | O que o produto acrescenta |
|---|---|
| `src/Atlas.Web/Program.cs` | Três linhas: `using Atlas.Web.Business;`, `builder.AddIndoor();` e `app.UseIndoor();` (depois de `UseAuthorization`). A composição fica em `Atlas.Web/Business/IndoorComposicao.cs` e `Atlas.Infrastructure/Business/DependencyInjection.cs` (serviços e políticas). O arquivo é **ISO-8859-1**: edite sem converter a codificação |
| `src/Atlas.Application/Common/Interfaces/IApplicationDbContext.cs` | Blocos `// Business — Parceiros` e `// Business — Mídia Indoor` com os `DbSet` de negócio |
| `src/Atlas.Infrastructure/Data/ApplicationDbContext.cs` | O mesmo bloco, na implementação |
| `src/Atlas.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs` | Modelo combinado Platform + Business (ADR-006) |
| `tests/Atlas.Infrastructure.UnitTests/*.csproj` | Pacote `Microsoft.EntityFrameworkCore.InMemory` 9.0.18 (já usado nos testes de integração) |

## Problemas conhecidos da versão adotada

Contornados no produto, sem alterar a plataforma:

| Problema | Efeito | Contorno |
|---|---|---|
| Login em modo **Bearer** emite a claim `sub` duplicada (corrigido no commit `ba81daf`, posterior à tag) | O próprio JwtBearer recusa o token: 401 em qualquer rota | O painel usa modo Cookie; o token das TVs é do módulo Indoor |
| O inspetor do histórico (`PostgreSqlEntityHistorySchemaInspector`) lê colunas `char(n)` como `character`, sem tamanho | A segunda subida da API acusa esquema incompatível na tabela histórica e não sobe | Entidades de negócio usam `varchar(n)` + check constraint, nunca `IsFixedLength()` |

## Histórico de atualizações

| Data | Versão anterior | Nova versão | Observações |
|---|---|---|---|
| `09/10/2026` | — | `v0.4.0-alpha.1` | Criação do PlayMenu (origem) |
| `13/10/2026` | — | `v0.4.0-alpha.1` | Mídia Indoor criada a partir do `playmenu-api`, sem o Business do PlayMenu |

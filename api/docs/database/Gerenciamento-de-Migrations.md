# Gerenciamento de Migrations

## Objetivo

Este documento define como a Atlas evolui, revisa e versiona a estrutura dos bancos gerenciados pelo Entity Framework Core.

O processo deve ser reproduzível, rastreável, revisável e aplicável de forma consistente em desenvolvimento, homologação e produção.

Este procedimento operacionaliza, sem substituir, os seguintes registros:

- `ADR-004`: Convenções de Idioma e Nomenclatura;
- `ADR-005`: Estratégia de Schemas no PostgreSQL;
- `ADR-006`: Contexto Único e Migrations Unificadas;
- `ADR-007`: Plataforma-base e Evolução dos Produtos de Cliente;
- `ADR-008`: Configuração de Ambientes e Segredos.

## Modelo adotado

Enquanto plataforma e negócio participarem do mesmo modelo relacional, o Atlas utilizará:

```text
Um ApplicationDbContext
+
Um ModelSnapshot
+
Uma sequência global de migrations
+
Uma tabela __EFMigrationsHistory
```

A origem arquitetural será identificada pelo nome:

```text
Platform_*
Business_*
Shared_*
```

Exemplos:

```text
Platform_CriarEstruturaInicial
Platform_AdicionarIndiceDeSessoes
Business_AdicionarEmpresas
Shared_AdicionarCamposDeAuditoria
```

O prefixo `Shared` será reservado a alterações técnicas verdadeiramente transversais.

## Conceitos fundamentais

### Modelo do EF Core

O modelo é formado por entidades, propriedades, relacionamentos, configurações `IEntityTypeConfiguration<T>`, mapeamentos do `OnModelCreating` e convenções reconhecidas pelo `ApplicationDbContext`.

O fluxo de mudança começa no modelo e nas configurações do código, não no banco.

### Migration

Uma migration registra a diferença entre o último modelo versionado e o modelo atual. Ela normalmente contém:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
```

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
```

`Up` descreve a evolução. `Down` descreve, quando tecnicamente viável, o retorno. O conteúdo gerado deve sempre ser revisado.

### ModelSnapshot

O `ModelSnapshot` representa o último modelo conhecido pela sequência de migrations. Ele:

- Deve ser versionado com a migration;
- Não representa o estado de um banco específico;
- Não substitui a tabela de histórico;
- Não deve ser alterado manualmente no fluxo normal.

### `__EFMigrationsHistory`

Cada banco registra nessa tabela quais migrations foram aplicadas. Ela permite que ambientes diferentes estejam em posições distintas da mesma sequência.

A tabela não deve ser editada manualmente no fluxo normal.

## Fonte de verdade

A estrutura oficial é definida pelo conjunto versionado:

```text
Entidades e configurações
+
Migrations
+
ModelSnapshot
```

Um banco alterado manualmente não se torna a definição oficial da estrutura.

## Fluxo obrigatório

Toda alteração estrutural deve seguir:

1. Alterar entidades e tipos;
2. Alterar configurações do EF Core;
3. Atualizar código dependente;
4. Compilar;
5. Gerar a migration;
6. Revisar `Up`, `Down`, metadata e snapshot;
7. Gerar e revisar o SQL;
8. Aplicar em banco local;
9. Executar testes;
10. Verificar mudanças pendentes;
11. Versionar modelo, migration e snapshot juntos;
12. Submeter a revisão;
13. Gerar o artefato de implantação quando necessário.

Não será fluxo padrão:

```text
Alterar o banco manualmente
→ adaptar o código depois
```

## Projetos utilizados

Na estrutura atual:

```text
Projeto das migrations e do DbContext:
src/Atlas.Infrastructure/Atlas.Infrastructure.csproj

Projeto de inicialização:
src/Atlas.Web/Atlas.Web.csproj

DbContext:
ApplicationDbContext

Diretório das migrations:
Data/Migrations
```

Os comandos pressupõem execução na raiz do repositório. Ajuste apenas os caminhos caso a estrutura física seja diferente.


## Execução das ferramentas do Entity Framework

O Atlas centraliza os resultados de compilação por meio da propriedade `ArtifactsPath`, definida no `Directory.Build.props`.

Nos builds normais, os resultados são direcionados para:

```text
artifacts/bin/
artifacts/obj/
```

Como o projeto que contém o `ApplicationDbContext` é diferente do projeto de inicialização, o EF CLI deve ser executado com a propriedade `AtlasEfTools` habilitada. Isso permite que cada projeto utilize temporariamente seu próprio diretório intermediário convencional.

Ative o modo EF no PowerShell:

```powershell
$env:AtlasEfTools = "true"
```

Restaure e compile na mesma sessão:

```powershell
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
```

Ao terminar os comandos do Entity Framework, remova a variável:

```powershell
Remove-Item Env:AtlasEfTools
```

Confirme:

```powershell
Test-Path Env:AtlasEfTools
```

O resultado esperado é:

```text
False
```

A variável existe apenas na sessão atual do PowerShell. Os diretórios locais `bin` e `obj` criados nesse modo permanecem ignorados pelo Git.

Não utilize `--msbuildprojectextensionspath` apontando o diretório intermediário de um único projeto quando o comando também carrega o projeto de inicialização.

## Ferramenta

Verificar:

```powershell
dotnet ef --version
```

Instalar, quando necessário:

```powershell
dotnet tool install --global dotnet-ef
```

Atualizar:

```powershell
dotnet tool update --global dotnet-ef
```

A versão principal da ferramenta deve ser compatível com os pacotes EF Core usados pela solução.

## Criação de migration

```powershell
dotnet ef migrations add <NOME> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext `
  --output-dir Data/Migrations
```

A primeira migration da plataforma será:

```text
Platform_CriarEstruturaInicial
```

Ela deverá representar o modelo estrutural completo da plataforma-base, incluindo schemas, Identity, tabelas, colunas, chaves, índices, constraints e relacionamentos.

## Revisão da migration

### `Up`

Verificar:

- Criação dos schemas antes dos objetos dependentes;
- Schema correto;
- Tabelas e colunas em `snake_case`;
- Tipos, tamanhos e precisão;
- Nulabilidade;
- Valores padrão;
- Chaves e relacionamentos;
- Comportamentos de exclusão;
- Índices e unicidade;
- Constraints;
- Ordem das operações;
- Possível perda de dados.

### `Down`

Verificar:

- Se desfaz apenas a mudança correspondente;
- Se respeita dependências;
- Se causa perda de dados;
- Se o retorno é realmente viável.

O simples fato de existir um método `Down` não torna o rollback seguro.

### Snapshot

Mudanças inesperadas podem indicar convenção alterada, pacote atualizado, mapeamento removido ou alteração acidental em outra entidade.

## Revisão do SQL

Script completo:

```powershell
dotnet ef migrations script 0 `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Entre duas migrations:

```powershell
dotnet ef migrations script <ORIGEM> <DESTINO> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext `
  --output artifacts/migrations/Atualizacao.sql
```

Procurar especialmente:

- `DROP TABLE` e `DROP COLUMN`;
- Renomeações interpretadas como exclusão e criação;
- Conversões de tipo;
- Operações com grandes volumes;
- Índices potencialmente caros;
- Ausência de schema;
- SQL inesperado ou não transacional;
- Locks prolongados.

## Renomeações

Quando a intenção for renomear, revisar se o EF gerou operações adequadas, como:

```csharp
migrationBuilder.RenameColumn(...)
```

```csharp
migrationBuilder.RenameTable(...)
```

Não aceitar `drop/create` quando isso resultar em perda de dados.

## Aplicação local

Até a última migration:

```powershell
dotnet ef database update `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Até uma migration específica:

```powershell
dotnet ef database update <MIGRATION> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Retornar a banco sem migrations, apenas quando descartável:

```powershell
dotnet ef database update 0 `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

## Diagnóstico

Listar migrations:

```powershell
dotnet ef migrations list `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Verificar modelo sem migration correspondente:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Essa verificação deverá futuramente fazer parte da integração contínua.

## Remoção de migration

Pode ser removida quando for a última, ainda local, não compartilhada e não aplicada em ambiente compartilhado:

```powershell
dotnet ef migrations remove `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Depois, corrigir o modelo, gerar novamente e repetir a revisão.

Migrations já compartilhadas, aplicadas em homologação ou produção, ou incorporadas a uma versão da plataforma não devem ser reescritas. A correção será uma nova migration.

## Alterações manuais e drift

Alteração manual não é o fluxo padrão. Quando uma intervenção emergencial for inevitável:

1. Registrar motivo, ambiente e responsável;
2. Preservar o SQL;
3. Fazer backup;
4. Reproduzir a mudança no modelo;
5. Criar migration de reconciliação;
6. Testar em ambiente equivalente;
7. Garantir que os demais ambientes recebam a mesma evolução.

Não editar `__EFMigrationsHistory` apenas para esconder divergência.

Drift deve ser tratado comparando schema real, modelo e histórico. A correção precisa ser testada antes de ser aplicada.

## Branches concorrentes

Quando duas branches criarem migrations a partir do mesmo snapshot:

1. Preservar a migration integrada primeiro;
2. Remover localmente a migration ainda não compartilhada;
3. Atualizar a branch;
4. Gerar novamente sobre o snapshot atualizado;
5. Revisar migration, snapshot e SQL;
6. Testar em banco limpo e sobre a versão anterior.

Não resolver conflito de snapshot escolhendo um lado sem regeneração.

## Seeds e dados

```text
Migration → estrutura e evolução do schema
Seed → dados necessários, de referência ou de inicialização
```

Seeds devem ser idempotentes, reexecutáveis, compatíveis com bancos existentes e independentes de `EnsureDeleted`.

Categorias:

- Dados técnicos obrigatórios;
- Dados de referência;
- Dados demonstrativos;
- Credenciais iniciais.

Dados demonstrativos não pertencem à plataforma-base. Senhas e segredos não devem existir no código, migrations ou seeds.

`HasData` deve ser reservado a dados determinísticos controlados pelo modelo. Não deve ser usado para senhas, datas atuais, dados dependentes de serviços ou informações alteráveis por usuários.

## `EnsureCreated` e `EnsureDeleted`

Após a adoção de migrations, `EnsureCreatedAsync` e `EnsureDeletedAsync` não farão parte da inicialização normal.

Eles podem existir apenas em ferramentas explicitamente destrutivas e protegidas, destinadas a testes, demonstrações ou ambientes efêmeros.

A inicialização normal nunca deve destruir o banco.

## Aplicação automática no startup

Em `Development`, a aplicação pode chamar `MigrateAsync` para aplicar migrations locais pendentes antes da execução dos seeds.

Homologação e produção não aplicarão migrations automaticamente durante o startup.

A forma inicial de implantação será script SQL revisado, conforme o ADR-006.

`MigrateAsync` em ambientes não locais somente poderá ser adotado futuramente por meio de ferramenta controlada.

## Rollback

Rollback deve ser planejado antes da implantação. Avaliar:

- Compatibilidade da aplicação anterior;
- Impacto sobre dados;
- Backup e restauração;
- Possibilidade de script reverso;
- Estratégia de roll-forward.

Em muitas situações, uma nova migration corretiva é mais segura do que desfazer uma alteração destrutiva.

Mudanças arriscadas devem preferir implantação em etapas: adicionar estrutura compatível, migrar uso e dados e remover a estrutura antiga em release posterior.

## Operações destrutivas

Exigem revisão explícita:

- Excluir tabela ou coluna;
- Reduzir tamanho;
- Tornar coluna obrigatória;
- Alterar tipo ou precisão;
- Recriar índice grande;
- Mover tabela entre schemas;
- Alterar chave ou comportamento de exclusão.

Devem possuir avaliação de dados, backup, teste, plano de retorno ou correção e comunicação de implantação.

## Checklist do Pull Request

### Autor

- [ ] Nome conforme ADR-004;
- [ ] Prefixo arquitetural correto;
- [ ] Schema conforme ADR-005;
- [ ] Solução compilada;
- [ ] Comandos EF executados com `AtlasEfTools` habilitado;
- [ ] Migration contém apenas mudanças intencionais;
- [ ] `Up`, `Down` e snapshot revisados;
- [ ] SQL gerado e revisado;
- [ ] Banco local atualizado;
- [ ] Teste em banco limpo e sobre a versão anterior;
- [ ] Seeds idempotentes;
- [ ] Nenhum segredo incluído;
- [ ] Modelo, migration e snapshot no mesmo PR.

### Revisor

- [ ] Alteração começa no modelo;
- [ ] Schemas e nomes corretos;
- [ ] Índices e constraints coerentes;
- [ ] Renomeações preservam dados;
- [ ] SQL sem operações inesperadas;
- [ ] Locks e volume considerados;
- [ ] Histórico anterior não reescrito;
- [ ] Implantação definida.

## Ações proibidas

- Alterar produção sem registrar;
- Criar tabelas manualmente como fonte oficial;
- Editar o histórico para esconder divergência;
- Apagar migration aplicada em ambiente compartilhado;
- Versionar modelo sem migration correspondente;
- Versionar migration sem snapshot;
- Usar `EnsureCreated` no banco gerenciado por migrations;
- Armazenar segredo em migration ou seed;
- Aplicar migration não revisada em produção;
- Forçar `--msbuildprojectextensionspath` de um projeto em operação que também carrega outro projeto.

## Referências internas

- `docs/decisions/ADR-004-Convencoes-de-Idioma-e-Nomenclatura.md`
- `docs/decisions/ADR-005-Estrategia-de-Schemas-No-PostgreSQL.md`
- `docs/decisions/ADR-006-Contexto-Unico-e-Migrations-Unificadas.md`
- `docs/decisions/ADR-007-Plataforma-Base-e-Evolucao-Dos-Produtos.md`
- `docs/decisions/ADR-008-Configuracao-de-Ambientes-e-Segredos.md`
- `docs/development/Guia-Rapido-de-Migrations.md`
- `docs/deployment/Implantacao-de-Migrations.md`
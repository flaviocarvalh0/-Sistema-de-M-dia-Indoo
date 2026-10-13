# Guia Rápido de Migrations

## Objetivo

Este guia reúne os comandos cotidianos para trabalhar com migrations no Atlas.

Para regras completas e cenários excepcionais, consulte:

```text
docs/database/Gerenciamento-de-Migrations.md
```

## Estrutura

```text
DbContext: ApplicationDbContext
Projeto das migrations: src/Atlas.Infrastructure/Atlas.Infrastructure.csproj
Projeto de inicialização: src/Atlas.Web/Atlas.Web.csproj
Diretório: Data/Migrations
```

Os comandos pressupõem execução na raiz do repositório.

## Antes de começar

```powershell
dotnet ef --version
dotnet build
```

Confirme que os User Secrets locais possuem:

```text
ConnectionStrings:AtlasDb
Jwt:Key
PlatformSettings:InitialAdminPassword
```

Ative o modo das ferramentas EF:

```powershell
$env:AtlasEfTools = "true"
```

Restaure e compile na mesma sessão:

```powershell
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
```

O Atlas centraliza os builds em `artifacts/`. O modo `AtlasEfTools` permite que o projeto das migrations e o projeto de inicialização utilizem temporariamente seus próprios diretórios `obj`.

## Fluxo diário

```text
Alterar modelo e configurações
↓
Compilar
↓
Gerar migration
↓
Revisar migration e snapshot
↓
Gerar e revisar SQL
↓
Aplicar localmente
↓
Testar
↓
Enviar modelo, migration e snapshot juntos
```

## Criar migration

Plataforma:

```powershell
dotnet ef migrations add Platform_<Descricao> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext `
  --output-dir Data/Migrations
```

Negócio:

```powershell
dotnet ef migrations add Business_<Descricao> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext `
  --output-dir Data/Migrations
```

Alteração transversal:

```powershell
dotnet ef migrations add Shared_<Descricao> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext `
  --output-dir Data/Migrations
```

Use `Shared` somente quando a mudança for realmente transversal.

## Revisar arquivos

Revise:

```text
<timestamp>_<Nome>.cs
<timestamp>_<Nome>.Designer.cs
ApplicationDbContextModelSnapshot.cs
```

Procure especialmente:

```text
DropTable
DropColumn
AlterColumn
RenameColumn
RenameTable
```

Confirme schema, nomes, tipos, nulabilidade, índices, relacionamentos e constraints.

## Gerar SQL

Migration inicial:

```powershell
dotnet ef migrations script 0 Platform_CriarEstruturaInicial `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext `
  --output artifacts/migrations/Platform_CriarEstruturaInicial.sql
```

Entre duas migrations:

```powershell
dotnet ef migrations script <ORIGEM> <DESTINO> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext `
  --output artifacts/migrations/Atualizacao.sql
```

## Aplicar localmente

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

## Listar migrations

```powershell
dotnet ef migrations list `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

## Verificar mudança sem migration

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Antes de concluir, não deve haver mudança pendente.

## Corrigir última migration ainda local

Somente quando ela não foi compartilhada ou aplicada em ambiente compartilhado:

```powershell
dotnet ef migrations remove `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Depois corrija o modelo, gere e revise novamente.

## Voltar banco local

```powershell
dotnet ef database update <MIGRATION_ANTERIOR> `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Banco descartável sem migrations:

```powershell
dotnet ef database update 0 `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

## Migration já compartilhada

Não remova nem reescreva. Crie uma nova migration corretiva.

## Conflito entre branches

1. Preserve a migration integrada primeiro;
2. Remova a migration ainda local da outra branch;
3. Atualize a branch;
4. Gere novamente;
5. Revise snapshot e SQL;
6. Teste em banco limpo e atualizado.

Não escolha arbitrariamente um lado do snapshot.

## Executar e validar os seeds

Depois de aplicar as migrations, execute:

```powershell
dotnet run --project src/Atlas.Web/Atlas.Web.csproj
```

Na primeira execução, valide a criação do grupo Administradores, do usuário `admin`, do vínculo entre eles, dos metadados e dos ícones.

Encerre e execute novamente. Na segunda execução, confirme que o banco foi preservado e que nenhum dado técnico foi duplicado.

## Encerrar o modo EF

Ao terminar:

```powershell
Remove-Item Env:AtlasEfTools
```

Confirme:

```powershell
Test-Path Env:AtlasEfTools
```

O resultado esperado é `False`.

## Checklist

- [ ] Ativei `AtlasEfTools`;
- [ ] Alterei o modelo e configurações;
- [ ] Compilei;
- [ ] Nomeei corretamente;
- [ ] Revisei `Up`, `Down` e snapshot;
- [ ] Revisei o SQL;
- [ ] Apliquei localmente;
- [ ] Testei;
- [ ] Executei os seeds duas vezes sem duplicações;
- [ ] Verifiquei mudanças pendentes;
- [ ] Enviei modelo, migration e snapshot juntos;
- [ ] Não incluí segredos;
- [ ] Removi `AtlasEfTools` ao terminar.
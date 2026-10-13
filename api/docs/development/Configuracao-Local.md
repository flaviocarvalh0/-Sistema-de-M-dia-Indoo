# Configuração Local

## Objetivo

Este guia descreve os passos necessários para executar localmente a aplicação Atlas sem inserir credenciais nos arquivos versionados.

Para conhecer a política completa, consulte:

```text
docs/deployment/Configuracao-de-Ambientes-e-Segredos.md
```

## Pré-requisitos

- .NET 9 SDK;
- PostgreSQL;
- Git;
- Acesso autorizado às credenciais de desenvolvimento.

## Projeto executável

Os comandos deste documento pressupõem execução na raiz do repositório.

O projeto executável é:

```text
src/Atlas.Web/Atlas.Web.csproj
```

Os comandos de User Secrets utilizam `--project` para evitar que os valores sejam associados ao projeto incorreto.

## Ambiente

Confirme que o perfil local utiliza:

```text
ASPNETCORE_ENVIRONMENT=Development
```

Essa configuração normalmente fica em:

```text
Properties/launchSettings.json
```

## Inicializar User Secrets

Execute uma única vez:

```powershell
dotnet user-secrets init `
  --project src/Atlas.Web/Atlas.Web.csproj
```

O comando adicionará um `UserSecretsId` ao arquivo `.csproj`.

## Configurar a conexão local

```powershell
dotnet user-secrets set `
  "ConnectionStrings:AtlasDb" `
  "CONNECTION_STRING_LOCAL" `
  --project src/Atlas.Web/Atlas.Web.csproj
```

Exemplo de formato:

```text
User Id=postgres;Password=<SENHA>;Host=localhost;Port=5432;Database=Atlas;Application Name=Atlas;Connection Idle Lifetime=60;
```

Não copie a senha real para os arquivos `appsettings`.

## Configurar a chave JWT

```powershell
dotnet user-secrets set `
  "Jwt:Key" `
  "CHAVE_JWT_LOCAL" `
  --project src/Atlas.Web/Atlas.Web.csproj
```

A chave deve ser longa, aleatória e exclusiva do ambiente de desenvolvimento.

## Configurar a senha inicial do Administrador

```powershell
dotnet user-secrets set `
  "PlatformSettings:InitialAdminPassword" `
  "SENHA_ADMIN_LOCAL" `
  --project src/Atlas.Web/Atlas.Web.csproj
```

Essa senha será utilizada pelo inicializador da plataforma na primeira execução para criar o usuário `admin` padrão do sistema.

## Conferir os valores

```powershell
dotnet user-secrets list `
  --project src/Atlas.Web/Atlas.Web.csproj
```

A saída deverá apresentar, no mínimo:

```text
ConnectionStrings:AtlasDb
Jwt:Key
PlatformSettings:InitialAdminPassword
```

## Restaurar e compilar

```powershell
dotnet restore Atlas.sln
dotnet build Atlas.sln --no-restore
```

## Preparar migrations localmente

O Atlas centraliza os resultados de build por meio de `ArtifactsPath`. Para executar comandos do Entity Framework, ative:

```powershell
$env:AtlasEfTools = "true"
```

Restaure na mesma sessão:

```powershell
dotnet restore Atlas.sln
```

Exemplo para aplicar todas as migrations:

```powershell
dotnet ef database update `
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj `
  --startup-project src/Atlas.Web/Atlas.Web.csproj `
  --context ApplicationDbContext
```

Ao terminar:

```powershell
Remove-Item Env:AtlasEfTools
```

## Executar a aplicação

```powershell
dotnet run --project src/Atlas.Web/Atlas.Web.csproj
```

Na primeira execução em banco vazio, valide a criação do administrador, do grupo Administradores, dos metadados e dos ícones.

Encerre e execute novamente para confirmar que os seeds são idempotentes e que o banco não é destruído ou recriado.

## Alterar um segredo

O comando `set` também atualiza um valor existente:

```powershell
dotnet user-secrets set `
  "Jwt:Key" `
  "NOVA_CHAVE_LOCAL" `
  --project src/Atlas.Web/Atlas.Web.csproj
```

## Remover um segredo

```powershell
dotnet user-secrets remove `
  "Jwt:Key" `
  --project src/Atlas.Web/Atlas.Web.csproj
```

## Limpar todos os segredos

```powershell
dotnet user-secrets clear `
  --project src/Atlas.Web/Atlas.Web.csproj
```

## Problemas comuns

### Connection string não encontrada

Confirme:

- Se o comando foi executado na pasta do projeto correto;
- Se o `.csproj` contém `UserSecretsId`;
- Se a chave foi criada como `ConnectionStrings:AtlasDb`;
- Se o ambiente atual é `Development`.

### Chave JWT não encontrada

Confirme:

- Se a chave foi criada como `Jwt:Key`;
- Se não existem espaços extras no nome;
- Se a aplicação está lendo `Jwt:Key`;
- Se o projeto executado é o mesmo que possui o `UserSecretsId`.

### Aplicação funciona na IDE, mas não no terminal

A IDE pode estar utilizando um perfil do `launchSettings.json`.

Confirme o ambiente no terminal e execute o projeto correto.


### Erro `GetEFProjectMetadata`

Ative o modo específico do Atlas:

```powershell
$env:AtlasEfTools = "true"
dotnet restore Atlas.sln
```

Execute novamente o comando do EF sem forçar `--msbuildprojectextensionspath`.

Não aponte o diretório intermediário de `Atlas.Infrastructure` para uma operação que também carrega `Atlas.Web`.

## Regras

- Não inserir segredos no `appsettings.json`;
- Não inserir segredos no `appsettings.Development.json`;
- Não enviar segredos por commit;
- Não reutilizar credenciais de produção;
- Não registrar segredos em logs;
- Comunicar imediatamente qualquer exposição.

## Checklist após clonar

- [ ] Dependências instaladas;
- [ ] Banco local disponível;
- [ ] Ambiente `Development` confirmado;
- [ ] `ConnectionStrings:AtlasDb` configurada;
- [ ] `Jwt:Key` configurada;
- [ ] `PlatformSettings:InitialAdminPassword` configurada;
- [ ] Migrations aplicadas;
- [ ] Aplicação executada com sucesso;
- [ ] Segunda execução concluída sem duplicações.
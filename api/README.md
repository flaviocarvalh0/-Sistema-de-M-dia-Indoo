# Mídia Indoor — API

Gestão de TVs (digital signage) para parceiros — academias, lanchonetes,
clínicas —, construída sobre a **Atlas Platform** `v0.4.0-alpha.1` (ver
[Versão da Plataforma](docs/platform/Versao-da-Plataforma.md)).

Faz parte do monorepo `-Sistema-de-M-dia-Indoo`: o painel é a pasta `web/`
(consome o `@atlas/framework`) e o app das TVs é a pasta `player/` (Flutter).

## Executar localmente

Pré-requisitos: .NET SDK 9 e PostgreSQL.

1. **Segredos da API** (nunca em arquivo versionado): connection string do
   banco `Indoor`, chave JWT e senha inicial do `admin` — comandos em
   [Configuração local](docs/development/Configuracao-Local.md).
2. **Banco e migrations:** com `AtlasEfTools=true` na sessão, como no
   [Guia rápido de migrations](docs/development/Guia-Rapido-de-Migrations.md):

   ```powershell
   $env:AtlasEfTools = "true"
   dotnet build Atlas.sln
   dotnet ef database update --project src/Atlas.Infrastructure --startup-project src/Atlas.Web --no-build
   Remove-Item Env:AtlasEfTools
   ```

   Depois compile de novo **sem** a variável (as ferramentas do EF compilam em
   outra pasta).
3. **Subir a API:** `dotnet run --project src/Atlas.Web --launch-profile https`
   → `https://localhost:7216` (documentação em `/scalar`). A primeira subida
   cria o `admin`, os metadados e o histórico.

### Testes

```powershell
dotnet test tests/Atlas.Infrastructure.UnitTests
dotnet test tests/Atlas.Infrastructure.IntegrationTests
```

Os de integração usam o banco **`Indoor_Testes`** — nunca o de
desenvolvimento, porque apagam objetos de histórico de propósito. Prepare uma
vez com `docs/scripts/configurar-banco-testes.ps1` (grava o user-secret do
projeto de testes, cria o banco e aplica as migrations); os testes de negócio
aplicam sozinhos as migrations novas.

## Documentação do produto

- [Modelo de dados](docs/MODELO_DADOS.md)
- [Plano de implementação](../docs/PLANO_IMPLEMENTACAO.md) (monorepo inteiro)

## Uso

Este repositório é privado e destinado exclusivamente aos responsáveis pelo
projeto e aos colaboradores autorizados.

A ausência de uma licença pública para o código próprio do Atlas não concede
permissão para sua publicação, redistribuição ou sublicenciamento.

## Documentação da plataforma

- [Funcionalidades da plataforma](docs/platform/Funcionalidades-da-Plataforma.md)
- [Configuração local](docs/development/Configuracao-Local.md)
- [Guia rápido de migrations](docs/development/Guia-Rapido-de-Migrations.md)
- [Configuração de ambientes e gestão de segredos](docs/deployment/Configuracao-de-Ambientes-e-Segredos.md)
- [Implantação de migrations](docs/deployment/Implantacao-de-Migrations.md)
- [Gerenciamento de migrations](docs/database/Gerenciamento-de-Migrations.md)
- [Registros de decisões arquiteturais](docs/decisions/README.md)

## Componentes de terceiros

Partes da estrutura inicial deste projeto foram baseadas no
Clean Architecture Solution Template, de Jason Taylor, disponibilizado sob
a licença MIT.

Consulte:

- `NOTICE.md`
- `LICENSES/JasonTaylorDev-CleanArchitecture-MIT.txt`

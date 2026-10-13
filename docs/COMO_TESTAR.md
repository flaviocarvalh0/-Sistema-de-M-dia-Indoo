# Como testar na sua máquina

Enquanto o painel (`web/`) e o app das TVs (`player/`) não existem, a API é
testada pela documentação interativa (Scalar) ou pelo roteiro
`api/src/Atlas.Web/Indoor.http`.

## 1. Pré-requisitos

- .NET SDK 9 — `dotnet --list-sdks`
- PostgreSQL 14+ rodando localmente
- Certificado de desenvolvimento HTTPS confiável (o login é por cookie
  `Secure`): `dotnet dev-certs https --trust`
- Opcional: VS Code com a extensão **REST Client** (ou Visual Studio 2022)

## 2. Baixar o código

```powershell
git clone https://github.com/flaviocarvalh0/-Sistema-de-M-dia-Indoo.git
cd -Sistema-de-M-dia-Indoo
git checkout claude/indoor-media-digital-signage-9i8nn3
cd api
```

## 3. Segredos (User Secrets, uma vez)

```powershell
dotnet user-secrets set "ConnectionStrings:AtlasDb" "Host=localhost;Port=5432;Database=Indoor;Username=postgres;Password=<SENHA>" --project src/Atlas.Web
dotnet user-secrets set "Jwt:Key" "<uma frase longa e aleatória, 64+ caracteres>" --project src/Atlas.Web
dotnet user-secrets set "PlatformSettings:InitialAdminPassword" "Temp@12345" --project src/Atlas.Web
dotnet user-secrets set "Indoor:Armazenamento:Supabase:Url" "https://<projeto>.supabase.co" --project src/Atlas.Web
dotnet user-secrets set "Indoor:Armazenamento:Supabase:ChaveServico" "<chave secreta do Supabase>" --project src/Atlas.Web
```

## 4. Banco

```powershell
dotnet tool restore
$env:AtlasEfTools = "true"
dotnet build Atlas.sln
dotnet ef database update --project src/Atlas.Infrastructure --startup-project src/Atlas.Web --no-build
Remove-Item Env:AtlasEfTools
dotnet build Atlas.sln
```

## 5. Subir a API

```powershell
dotnet run --project src/Atlas.Web --launch-profile https
```

A primeira subida cria o usuário `admin` (senha do passo 3), o catálogo de
entidades, o histórico e o bucket `midias` no Supabase (confira em
*Storage* no painel do Supabase).

Depois aplique, uma vez, o script do Studio (módulos, menus e grupos):

```powershell
psql -h localhost -U postgres -d Indoor -f docs/scripts/studio-indoor.sql
```

## 6. Testar

**Pelo roteiro (recomendado):** abra `api/src/Atlas.Web/Indoor.http`,
coloque um `video.mp4` e uma `imagem.png` na mesma pasta e execute as
requisições de cima para baixo. O roteiro cadastra um parceiro, entra como o
dono, envia vídeo e imagem ao Supabase, monta a playlist, cria a tela e gera
o código de pareamento. Os passos 12 a 15 fazem o papel da TV: pareiam com o
código, sincronizam (as `url` da resposta abrem o arquivo) e mostram a tela
Online no painel. O contrato completo está em [CONTRATO_DISPOSITIVO.md](CONTRATO_DISPOSITIVO.md).

**Pela documentação interativa:** `https://localhost:7216/scalar`. Faça o
login em `POST /api/Auth/login` com `"mode": "Cookie"`; as demais chamadas
usam o cookie.

### O que conferir

- `GET /midias/{id}` traz `urlVisualizacao`: abra no navegador e o arquivo
  toca (vem do Supabase, válida por 1 h).
- Enviar um PNG como `video/mp4` é recusado e o arquivo some do bucket.
- Enviar além da cota do parceiro é recusado antes do upload.
- Excluir uma mídia que está numa playlist, ou uma playlist que está numa
  tela, é recusado.
- Salvar com uma `version` antiga devolve 409.
- O `admin` não enxerga o painel de parceiro (403): ele não pertence a um parceiro.
- Desvincular a TV no painel faz a sincronização dela responder 401; bloquear
  o parceiro, 403.

### Testes automatizados

```powershell
dotnet test tests/Atlas.Infrastructure.UnitTests
dotnet test tests/Atlas.Infrastructure.IntegrationTests   # banco Indoor_Testes, ver api/README.md
```

Para incluir o teste de contrato contra o seu Supabase (cria e apaga um
arquivo no bucket `midias-testes`):

```powershell
$env:INDOOR_SUPABASE_URL = "https://<projeto>.supabase.co"
$env:INDOOR_SUPABASE_CHAVE_SERVICO = "<chave secreta>"
dotnet test tests/Atlas.Infrastructure.IntegrationTests --filter "FullyQualifiedName~Contrato"
```

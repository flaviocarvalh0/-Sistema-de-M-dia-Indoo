# Prepara o banco dos testes de integracao (Indoor_Testes):
#   1. grava a connection string no cofre (user-secrets) do projeto de testes;
#   2. cria o banco, se nao existir, e aplica todas as migrations.
#
# Os testes de integracao APAGAM objetos de historico de proposito: nunca
# aponte esta connection string para o banco de desenvolvimento (Indoor).
#
# A senha e pedida no prompt e nao fica em arquivo nem no historico. Ela e
# passada ao dotnet pela linha de comando durante a execucao.
#
#   .\docs\scripts\configurar-banco-testes.ps1
#
# Arquivo so em ASCII: o Windows PowerShell 5.1 le .ps1 sem BOM como ANSI.
param(
    [string] $Banco = 'Indoor_Testes',
    [int] $Porta = 5434,
    [string] $Usuario = 'postgres'
)

$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$projetoTestes = Join-Path $raiz 'tests\Atlas.Infrastructure.IntegrationTests'

if ($Banco -eq 'Indoor') {
    throw 'O banco de testes nao pode ser o de desenvolvimento.'
}

$segura = Read-Host "Senha de $Usuario (porta $Porta)" -AsSecureString
$senha = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura))
$conexao = "Host=localhost;Port=$Porta;Database=$Banco;Username=$Usuario;Password=$senha"

try {
    Write-Host '1/3 Gravando a connection string nos user-secrets do projeto de testes...'
    dotnet user-secrets set 'ConnectionStrings:AtlasDb' $conexao --project $projetoTestes | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao gravar o user-secret.' }

    Write-Host '2/3 Compilando no modo das ferramentas do EF...'
    $env:AtlasEfTools = 'true'
    Push-Location $raiz
    dotnet build Atlas.sln -v q -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilacao.' }

    Write-Host "3/3 Criando $Banco e aplicando as migrations..."
    dotnet ef database update --project src/Atlas.Infrastructure --startup-project src/Atlas.Web --no-build --connection $conexao
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao aplicar as migrations.' }

    Write-Host "Pronto: $Banco criado e migrado."
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
    Remove-Item Env:AtlasEfTools -ErrorAction SilentlyContinue
    $senha = $null
    $conexao = $null
}

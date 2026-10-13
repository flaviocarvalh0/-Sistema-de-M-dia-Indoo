# Roda um script .sql num banco da Midia Indoor pedindo a senha no prompt (nao
# fica no historico nem em arquivo).
#
# Arquivo so em ASCII: o Windows PowerShell 5.1 le .ps1 sem BOM como ANSI.
#
#   Local (padrao):
#     .\docs\scripts\aplicar-script.ps1 .\docs\scripts\studio-rede.sql
#
#   Outro ambiente:
#     .\docs\scripts\aplicar-script.ps1 .\docs\scripts\studio-rede.sql `
#         -Servidor db.exemplo.com.br -Porta 5432 -Banco indoor -Usuario indoor_admin
param(
    [Parameter(Mandatory)] [string] $Arquivo,
    [string] $Servidor = 'localhost',
    [string] $Banco = 'Indoor',
    [int] $Porta = 5434,
    [string] $Usuario = 'postgres',
    [string] $Psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
)

Write-Host "Aplicando $Arquivo em $Servidor`:$Porta/$Banco"
$senha = Read-Host "Senha de $Usuario" -AsSecureString
$env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($senha))

try {
    & $Psql -h $Servidor -p $Porta -U $Usuario -d $Banco -v ON_ERROR_STOP=1 -f $Arquivo
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

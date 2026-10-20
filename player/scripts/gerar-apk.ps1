<#
.SYNOPSIS
    Sobe a versao do app das TVs no pubspec.yaml e gera o APK (debug, profile
    ou release) num passo so, pronto para enviar em Rede -> Telas -> Versoes
    do app.

.DESCRIPTION
    Faz, nesta ordem:
      1. le a versao do pubspec.yaml (ex.: 1.0.0+1);
      2. sobe a versao (por padrao o patch e o codigo: 1.0.1+2). O numero
         depois do "+" e o que a TV compara para se atualizar (ADR-021): ele
         SEMPRE sobe, senao a TV nao instala;
      3. roda o `flutter build apk` com o API_URL;
      4. copia os APKs para player/dist/ com a versao no nome. Por padrao gera
         UM POR ARQUITETURA (--split-per-abi): o universal passa dos 50 MB
         aceitos pela pagina Versoes do app. O armeabi-v7a e o que se envia:
         roda nas TV box de 32 e de 64 bits;
      5. imprime o que preencher em Versoes do app (arquivo, codigo, nome e
         "O que mudou");
      6. opcionalmente instala direto na TV pelo adb (-Instalar), escolhendo o
         APK da arquitetura do aparelho.

    Se o build falhar, o pubspec.yaml volta para a versao de antes: nada de
    numero queimado sem APK.

    QUAL MODO USAR
      Profile  (padrao) Para testar na rede local. Aceita o certificado de
               desenvolvimento do .NET (https://<IP do PC>:7216), e otimizado
               e pequeno. E o que voce envia em Versoes do app nos testes.
      Release  Para producao. So aceita certificado HTTPS VALIDO (dominio
               ou tunel da Cloudflare): uma TV de release apontando para o IP
               do PC para de falar com a API.
      Debug    Para depurar. Aceita o certificado de desenvolvimento, mas o
               APK costuma passar de 50 MB (nao sobe em Versoes do app);
               use com -Instalar.

    A atualizacao so instala por cima se o APK for assinado com a MESMA
    chave do que esta na TV. Hoje o projeto assina com a chave de debug da
    maquina: gere sempre no mesmo PC.

.PARAMETER Modo
    Debug, Profile ou Release. Padrao: Profile.

.PARAMETER ApiUrl
    Endereco da API gravado no app (so vale para instalacao nova; a TV ja
    pareada continua com o servidor que tem salvo). Padrao: a variavel de
    ambiente INDOOR_API_URL; sem ela, em Debug/Profile, https://<IPv4 deste
    PC>:7216. Em Release e obrigatorio e precisa ser https com dominio.

.PARAMETER Subir
    Que parte da versao sobe. O codigo (depois do "+") sobe sempre, menos
    em Nenhum.
      Patch  (padrao) 1.0.0+1 -> 1.0.1+2
      Minor            1.0.3+4 -> 1.1.0+5
      Major            1.4.2+9 -> 2.0.0+10
      Codigo           1.0.0+1 -> 1.0.0+2 (so o codigo)
      Nenhum           gera de novo a mesma versao (a TV NAO se atualiza)

.PARAMETER Universal
    Gera um APK so, com todas as arquiteturas (sem --split-per-abi). Fica
    acima de 50 MB: serve para instalar a mao (-Instalar), nao para enviar
    em Versoes do app.

.PARAMETER Notas
    Texto do campo "O que mudou" em Versoes do app. Opcional: o script so
    repete no resumo final, pronto para copiar.

.PARAMETER Instalar
    Instala o APK gerado pelo adb (adb install -r): o da arquitetura do
    aparelho (adb shell getprop ro.product.cpu.abi), ou o armeabi-v7a.

.PARAMETER Aparelho
    Serial ou IP:porta do aparelho para o adb (adb -s). Ex.: 192.168.1.50:5555.

.PARAMETER Forcar
    Em Release, aceita ApiUrl com IP ou localhost (o app nao vai conseguir
    falar com a API; use so se souber o que esta fazendo).

.EXAMPLE
    .\scripts\gerar-apk.ps1
    Profile para a rede local, sobe o patch: 1.0.0+1 -> 1.0.1+2.

.EXAMPLE
    .\scripts\gerar-apk.ps1 -Modo Release -ApiUrl https://tv.suaempresa.com.br
    APK de producao.

.EXAMPLE
    .\scripts\gerar-apk.ps1 -Modo Debug -Subir Codigo -Instalar -Aparelho 192.168.1.50:5555
    Debug instalado direto na TV.

.NOTES
    Rode de qualquer pasta: o script acha o player/ pelo proprio caminho.
    Requer o flutter no PATH (e o adb, com -Instalar).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Profile', 'Release')]
    [string]$Modo = 'Profile',

    [string]$ApiUrl,

    [ValidateSet('Patch', 'Minor', 'Major', 'Codigo', 'Nenhum')]
    [string]$Subir = 'Patch',

    [switch]$Universal,

    [string]$Notas,

    [switch]$Instalar,

    [string]$Aparelho,

    [switch]$Forcar
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$LimiteMb = 50
$Player = Split-Path -Parent $PSScriptRoot
$Pubspec = Join-Path $Player 'pubspec.yaml'
$Utf8SemBom = New-Object System.Text.UTF8Encoding $false

function Escrever([string]$texto, [string]$cor = 'Gray') { Write-Host $texto -ForegroundColor $cor }

function Falhar([string]$texto) {
    Write-Host ''
    Write-Host "ERRO: $texto" -ForegroundColor Red
    exit 1
}

# IPv4 privado da placa com gateway (Wi-Fi ou cabo): o endereco que a TV alcanca.
function Get-IpDaRede {
    $interfaces = [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
        Where-Object { $_.OperationalStatus -eq 'Up' -and $_.NetworkInterfaceType -ne 'Loopback' }

    foreach ($placa in $interfaces) {
        $propriedades = $placa.GetIPProperties()
        if (-not ($propriedades.GatewayAddresses | Where-Object { $_.Address.AddressFamily -eq 'InterNetwork' })) { continue }

        foreach ($endereco in $propriedades.UnicastAddresses) {
            $ip = $endereco.Address
            if ($ip.AddressFamily -ne 'InterNetwork') { continue }
            $b = $ip.GetAddressBytes()
            if ($b[0] -eq 10 -or ($b[0] -eq 192 -and $b[1] -eq 168) -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31)) {
                return $ip.ToString()
            }
        }
    }

    return $null
}

function Get-NovaVersao([int]$maior, [int]$menor, [int]$patch, [int]$codigo, [string]$parte) {
    switch ($parte) {
        'Major'  { return @(($maior + 1), 0, 0, ($codigo + 1)) }
        'Minor'  { return @($maior, ($menor + 1), 0, ($codigo + 1)) }
        'Patch'  { return @($maior, $menor, ($patch + 1), ($codigo + 1)) }
        'Codigo' { return @($maior, $menor, $patch, ($codigo + 1)) }
        default  { return @($maior, $menor, $patch, $codigo) }
    }
}

# ---------- Conferencias ----------

if (-not (Test-Path $Pubspec)) { Falhar "pubspec.yaml nao encontrado em $Player." }
if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) { Falhar 'flutter nao esta no PATH.' }
if ($Instalar -and -not (Get-Command adb -ErrorAction SilentlyContinue)) { Falhar 'adb nao esta no PATH (necessario para -Instalar).' }

if (-not $ApiUrl) { $ApiUrl = $env:INDOOR_API_URL }
if (-not $ApiUrl) {
    if ($Modo -eq 'Release') { Falhar 'Em Release informe -ApiUrl (https com dominio, ex.: https://tv.suaempresa.com.br).' }
    $ip = Get-IpDaRede
    if (-not $ip) { Falhar 'Nao achei o IP deste PC na rede. Informe -ApiUrl https://<IP>:7216.' }
    $ApiUrl = "https://${ip}:7216"
}

$uri = $null
if (-not [System.Uri]::TryCreate($ApiUrl, [System.UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http', 'https')) {
    Falhar "ApiUrl invalida: $ApiUrl"
}

if ($Modo -eq 'Release' -and -not $Forcar) {
    $semCertificadoValido = $uri.Scheme -ne 'https' -or $uri.IsLoopback -or $uri.HostNameType -in @('IPv4', 'IPv6')
    if ($semCertificadoValido) {
        Falhar ("O APK de release so aceita certificado HTTPS valido, e $ApiUrl nao tem um. " +
            'Use o dominio ou o tunel (https://...), ou gere -Modo Profile para testar na rede local.')
    }
}

# ---------- Versao ----------

$original = [System.IO.File]::ReadAllText($Pubspec)
$achado = [regex]::Match($original, '(?m)^version:[ \t]*(\d+)\.(\d+)\.(\d+)\+(\d+)[ \t]*\r?$')
if (-not $achado.Success) { Falhar 'Linha "version: X.Y.Z+N" nao encontrada no pubspec.yaml.' }

$atual = '{0}.{1}.{2}+{3}' -f $achado.Groups[1].Value, $achado.Groups[2].Value, $achado.Groups[3].Value, $achado.Groups[4].Value
$v = Get-NovaVersao ([int]$achado.Groups[1].Value) ([int]$achado.Groups[2].Value) ([int]$achado.Groups[3].Value) ([int]$achado.Groups[4].Value) $Subir
$nome = '{0}.{1}.{2}' -f $v[0], $v[1], $v[2]
$codigo = $v[3]
$nova = "$nome+$codigo"

Escrever ''
Escrever "App das TVs  $atual  ->  $nova   ($Modo)" 'Cyan'
Escrever "API_URL      $ApiUrl"
if ($Subir -eq 'Nenhum') { Escrever 'Aviso: versao nao subiu. A TV so se atualiza com codigo maior que o instalado.' 'Yellow' }
if ($Modo -eq 'Debug') { Escrever "Aviso: APK debug costuma passar de $LimiteMb MB e nao sobe em Versoes do app. Para testar a atualizacao use Profile." 'Yellow' }
Escrever ''

$alterado = $false
if ($nova -ne $atual) {
    $linhaNova = 'version: ' + $nova
    $conteudo = [regex]::Replace($original, '(?m)^version:[ \t]*\d+\.\d+\.\d+\+\d+[ \t]*(?=\r?$)', $linhaNova, 1)
    [System.IO.File]::WriteAllText($Pubspec, $conteudo, $Utf8SemBom)
    $alterado = $true
}

function Restaurar {
    if ($alterado) {
        [System.IO.File]::WriteAllText($Pubspec, $original, $Utf8SemBom)
        Escrever "pubspec.yaml voltou para $atual." 'Yellow'
    }
}

# ---------- Build ----------

$argumentos = @('build', 'apk', "--$($Modo.ToLowerInvariant())", "--dart-define=API_URL=$ApiUrl")
if (-not $Universal) { $argumentos += '--split-per-abi' }

Push-Location $Player
try {
    Escrever ("flutter " + ($argumentos -join ' ')) 'DarkGray'
    & flutter @argumentos
    $saida = $LASTEXITCODE
}
catch {
    $saida = 1
    Write-Host $_ -ForegroundColor Red
}
finally {
    Pop-Location
}

if ($saida -ne 0) {
    Restaurar
    Falhar "flutter build falhou (codigo $saida)."
}

# ---------- Copia com a versao no nome ----------

$modoMinusculo = $Modo.ToLowerInvariant()
$pastaBuild = Join-Path $Player 'build/app/outputs/flutter-apk'
$gerados = @(Get-ChildItem -Path $pastaBuild -Filter "app*-$modoMinusculo.apk" -ErrorAction SilentlyContinue |
    Where-Object { if ($Universal) { $_.Name -eq "app-$modoMinusculo.apk" } else { $_.Name -ne "app-$modoMinusculo.apk" } })
if ($gerados.Count -eq 0) {
    Restaurar
    Falhar "Nenhum APK encontrado em $pastaBuild."
}

$dist = Join-Path $Player 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$copiados = @()
foreach ($apk in $gerados) {
    $abi = $apk.BaseName -replace "^app-?", '' -replace "-?$modoMinusculo$", ''
    $sufixo = if ($abi) { "-$abi" } else { '' }
    $destino = Join-Path $dist ("midia-indoor-$nome+$codigo-$modoMinusculo$sufixo.apk")
    Copy-Item -Path $apk.FullName -Destination $destino -Force
    $copiados += Get-Item $destino
}

Escrever ''
Escrever 'APK gerado:' 'Green'
foreach ($arquivo in $copiados) {
    $mb = [math]::Round($arquivo.Length / 1MB, 1)
    $cor = if ($mb -gt $LimiteMb) { 'Yellow' } else { 'Green' }
    Escrever ("  {0}  ({1} MB)" -f $arquivo.FullName, $mb) $cor
    if ($mb -gt $LimiteMb) {
        Escrever "  Passa de $LimiteMb MB: Versoes do app recusa. Gere de novo sem -Universal (e -Subir Nenhum para manter $nova)." 'Yellow'
    }
}

# ---------- Instalacao (opcional) ----------

# O que se envia em Versoes do app: o armeabi-v7a roda em TV de 32 e de 64 bits.
$paraEnviar = $copiados | Where-Object { $_.Name -like '*armeabi-v7a*' } | Select-Object -First 1
if (-not $paraEnviar) { $paraEnviar = $copiados | Select-Object -First 1 }

if ($Instalar) {
    $adb = @()
    if ($Aparelho) {
        if ($Aparelho -match ':\d+$') { & adb connect $Aparelho | Out-Host }
        $adb += @('-s', $Aparelho)
    }

    # O APK da arquitetura do aparelho, se houver; senao o armeabi-v7a.
    $abiAparelho = (& adb @adb shell getprop ro.product.cpu.abi 2>$null | Out-String).Trim()
    $alvo = $copiados | Where-Object { $abiAparelho -and $_.Name -like "*-$abiAparelho.apk" } | Select-Object -First 1
    if (-not $alvo) { $alvo = $paraEnviar }
    if ($abiAparelho) { Escrever "Aparelho: $abiAparelho" 'DarkGray' }

    $adb += @('install', '-r', $alvo.FullName)

    Escrever ''
    Escrever ("adb " + ($adb -join ' ')) 'DarkGray'
    & adb @adb
    if ($LASTEXITCODE -ne 0) {
        Escrever 'adb install falhou. "INSTALL_FAILED_UPDATE_INCOMPATIBLE" = assinatura diferente da do app na TV.' 'Yellow'
    }
}

# ---------- Proximos passos ----------

Escrever ''
Escrever 'Em Rede -> Telas -> Versoes do app, preencha:' 'Cyan'
Escrever ("  Arquivo APK       " + $paraEnviar.FullName) 'White'
Escrever ("  Codigo da versao  $codigo") 'White'
Escrever ("  Nome              $nome") 'White'
Escrever ("  O que mudou       " + $(if ($Notas) { $Notas } else { '(opcional) o que a TV ganhou nesta versao' })) 'White'
if ($Universal) { Escrever '  (APK universal: acima de 50 MB a pagina recusa)' 'Yellow' }
Escrever ''
Escrever 'Depois:' 'Cyan'
Escrever '  1. Marque a tela como "TV de teste" e libere "So TVs de teste". Em 1-2 min a TV instala.'
Escrever '  2. Deu certo? Mude para "Todas as TVs".'
if ($alterado) { Escrever "  3. Faca o commit do pubspec.yaml ($nova): git add player/pubspec.yaml; git commit -m `"chore(player): versao $nova`"" }

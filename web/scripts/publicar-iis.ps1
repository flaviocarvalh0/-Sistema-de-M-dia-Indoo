<#
.SYNOPSIS
    Compila o SPA e substitui o conteudo da pasta do site no IIS — local ou
    num servidor remoto, com backup e rollback.

.DESCRIPTION
    Faz num passo o que antes era build -> copiar -> apagar -> colar.

    A copia e um ESPELHO (robocopy /MIR): o que nao existe mais no build e
    apagado do destino. Isso e obrigatorio, nao zelo. Os chunks levam hash no
    nome (`outputHashing: all`), entao um deploy que so sobrescreve vai
    acumulando os arquivos das versoes anteriores para sempre.

    DESTINO REMOTO: passe um caminho UNC (\\servidor\c$\inetpub\midia-indoor-web). O
    robocopy fala UNC nativamente — nao e preciso instalar nada no servidor,
    so ter acesso de escrita ao compartilhamento. Use -Credencial quando a
    conta do Windows local nao for a mesma que tem permissao la.

    BACKUP: antes de espelhar, a versao que esta no ar e copiada para uma
    pasta datada. `-Reverter` restaura a mais recente. Sem isso, um /MIR com
    build quebrado deixaria producao no chao sem caminho de volta.

    O `web.config` mora em `public/` e o build o copia para a raiz do `dist`,
    entao ele viaja junto e nao precisa de tratamento especial aqui.

.PARAMETER Destino
    Pasta fisica do site. Local (C:\inetpub\wwwroot) ou UNC
    (\\servidor\c$\inetpub\wwwroot). Default: C:\inetpub\wwwroot

.PARAMETER BaseHref
    Caminho do app dentro do site, gravado no <base href> do index.html.

    DERIVADO DO DESTINO quando omitido: um destino terminado em
    ...\wwwroot\midia-indoor-web vira "/midia-indoor-web/"; wwwroot direto vira "/". Isso existe
    porque errar aqui nao da erro, da TELA BRANCA — o index.html pede os
    arquivos a partir do caminho errado e todos voltam 404. O valor usado e
    sempre impresso antes do build.

    Informe manualmente quando o app estiver numa subpasta que nao seja
    filha de wwwroot, ou quando o site do IIS apontar para outro lugar.

.PARAMETER Empacotar
    Nao publica: compila e gera um ZIP para levar ao servidor a mao.

    Existe porque o build PRECISA sair desta maquina — o @atlas/framework
    entra por symlink para uma pasta fora do repositorio, entao um servidor
    sem os dois repositorios nao consegue compilar. O servidor so recebe
    estatico.

    O ZIP leva o app dentro de `app\` e um `instalar-no-servidor.cmd` que
    faz o espelhamento com backup no destino. Assim o lado de la tambem e
    um comando so, e ninguem precisa lembrar de apagar os arquivos antigos.

    Exige -BaseHref explicito: sem destino em disco nao ha o que derivar, e
    errar esse valor da TELA BRANCA no servidor.

.PARAMETER Credencial
    Credencial para o compartilhamento remoto. Obtenha com Get-Credential.
    Ignorada em destino local.

.PARAMETER PastaBackup
    Onde guardar as versoes anteriores. Default: pasta `_backups-midia-indoor-web` ao lado
    do destino.

.PARAMETER ManterBackups
    Quantas versoes anteriores preservar. Default: 3. Zero desliga o backup.

.PARAMETER ComFramework
    Recompila o @atlas/framework antes. Use quando mexeu na biblioteca — o
    `dist` dela e consumido por symlink, e sem rebuild o SPA compila contra a
    versao anterior sem reclamar.

.PARAMETER Reverter
    Nao compila nada: restaura o backup mais recente por cima do destino.

.PARAMETER Forcar
    Pula a checagem de seguranca do destino. Ver a nota em VALIDACAO.

.PARAMETER SemConfirmar
    Pula a confirmacao exigida em destino remoto. Para uso em automacao.

.EXAMPLE
    .\scripts\publicar-iis.ps1
    Publica no site padrao do IIS. EXIGE PowerShell como Administrador:
    C:\inetpub so aceita escrita com sessao elevada.

.EXAMPLE
    .\scripts\publicar-iis.ps1 -Destino "\\srv-web\c$\inetpub\wwwroot"

.EXAMPLE
    .\scripts\publicar-iis.ps1 -Destino "\\srv-web\midia-indoor-web" -Credencial (Get-Credential)

.EXAMPLE
    .\scripts\publicar-iis.ps1 -Destino "\\srv-web\c$\inetpub\wwwroot" -Reverter
    Volta para a versao anterior.
#>
[CmdletBinding()]
param(
    [string]       $Destino = 'C:\inetpub\wwwroot\midia-indoor-web',
    [string]       $BaseHref,
    [pscredential] $Credencial,
    [string]       $PastaBackup,
    [int]          $ManterBackups = 3,
    [ValidateSet('patch','minor','major','nenhuma')]
    [string]       $Versao = 'patch',
    [switch]       $ComFramework,
    [switch]       $Empacotar,
    [switch]       $Reverter,
    [switch]       $Forcar,
    [switch]       $SemConfirmar
)

$ErrorActionPreference = 'Stop'

$raizProjeto = Split-Path -Parent $PSScriptRoot
$origem      = Join-Path $raizProjeto 'dist\midia-indoor-web\browser'
# Procura a pasta do framework SUBINDO a partir do projeto, em vez de contar
# niveis fixos. Contar niveis quebrou na primeira vez que -ComFramework rodou:
# o script assumia dois acima (\AF\atlas-framework) e a pasta esta em
# \Documents\atlas-framework. Procurar pelo package.json acha nos dois casos.
$raizFw = $null
$cursor = $raizProjeto
while ($cursor) {
    $tentativa = Join-Path $cursor 'atlas-framework'
    if (Test-Path (Join-Path $tentativa 'package.json')) { $raizFw = $tentativa; break }
    $cursor = Split-Path -Parent $cursor
}
$remoto      = $Destino.StartsWith('\\')

# Deriva o <base href> do destino: o que vem depois de \wwwroot\ e o caminho
# pelo qual o IIS vai servir o app. Funciona igual em caminho local e UNC.
if (-not $BaseHref) {
    if ($Destino -match '(?i)\\wwwroot\\(.+)$') {
        $BaseHref = '/' + (($Matches[1] -replace '\\', '/').Trim('/')) + '/'
    } else {
        $BaseHref = '/'
    }
}
if (-not $BaseHref.EndsWith('/')) { $BaseHref = $BaseHref + '/' }

if (-not $PastaBackup) {
    $PastaBackup = Join-Path (Split-Path -Parent $Destino) '_backups-midia-indoor-web'
}

# No modo pacote nao ha destino em disco de onde derivar o base href, e
# errar esse valor nao da erro: da tela branca com 404 nos chunks. Por isso
# ele e obrigatorio aqui, em vez de assumir "/".
if ($Empacotar -and -not $PSBoundParameters.ContainsKey('BaseHref')) {
    Write-Host "`nABORTADO: -Empacotar exige -BaseHref explicito.`n" -ForegroundColor Red
    Write-Host "  O valor e o caminho pela URL, nao o caminho em disco:" -ForegroundColor Yellow
    Write-Host "    site em http://servidor:4200/        ->  -BaseHref `"/`""
    Write-Host "    site em http://servidor/midia-indoor-web/         ->  -BaseHref `"/midia-indoor-web/`"`n"
    exit 1
}

function Escrever($texto, $cor = 'Cyan') { Write-Host "`n>> $texto" -ForegroundColor $cor }
function Falhar($texto) { Write-Host "`nABORTADO: $texto`n" -ForegroundColor Red; exit 1 }

# Robocopy NAO usa a convencao "0 = sucesso": 0..7 sao resultados normais
# (1 = arquivos copiados, 2 = extras removidos, 3 = os dois...). Só de 8 para
# cima e falha de verdade. Sem esta traducao, o script acusaria erro em toda
# publicacao bem-sucedida.
function Espelhar($de, $para) {
    & robocopy $de $para /MIR /R:2 /W:2 /NFL /NDL /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Robocopy falhou (codigo $LASTEXITCODE) copiando '$de' -> '$para'."
    }
}

# Um teste de escrita real vale mais que inferir por caminho: `C:\inetpub`
# exige elevacao, mas a pasta pode ter sido liberada para o usuario, e um
# compartilhamento remoto pode negar escrita por mil outros motivos.
function PodeEscrever($pasta) {
    $teste = Join-Path $pasta ('.escrita-{0}.tmp' -f [guid]::NewGuid().ToString('N').Substring(0, 8))
    try {
        New-Item -ItemType File -Path $teste -ErrorAction Stop | Out-Null
        Remove-Item -LiteralPath $teste -Force -ErrorAction SilentlyContinue
        return $true
    } catch { return $false }
}

function EstouElevado {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$id).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function TamanhoDe($pasta) {
    $soma = (Get-ChildItem -LiteralPath $pasta -Recurse -File -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
    if (-not $soma) { return '0 MB' }
    return '{0:N1} MB' -f ($soma / 1MB)
}

# =========================================================================
# CONEXAO COM O COMPARTILHAMENTO
# =========================================================================
# `New-PSDrive` e o jeito de carregar credencial para um UNC: o robocopy nao
# aceita usuario/senha, ele usa a sessao ja autenticada do Windows. Montar o
# drive cria essa sessao; o robocopy continua recebendo o caminho UNC cru.
$driveTemporario = $null
if ($remoto -and $Credencial) {
    $raizShare = ($Destino -split '\\') | Where-Object { $_ } | Select-Object -First 2
    $raizShare = '\\' + ($raizShare -join '\')
    try {
        $driveTemporario = New-PSDrive -Name 'PubMidiaIndoor' -PSProvider FileSystem `
            -Root $raizShare -Credential $Credencial -ErrorAction Stop
    } catch {
        Falhar "Nao foi possivel autenticar em '$raizShare'.`n$($_.Exception.Message)"
    }
}

try {

    # No modo pacote nao ha destino em disco: alcance, permissao, validacao,
    # backup e espelhamento nao se aplicam. So compila e zipa.
    if (-not $Empacotar) {

    # =====================================================================
    # ALCANCE
    # =====================================================================
    # Falha cedo e com mensagem clara. Sem isto, um servidor fora do ar so
    # apareceria como erro de robocopy DEPOIS de um build de 20 segundos.
    $paiDoDestino = Split-Path -Parent $Destino
    if (-not (Test-Path $paiDoDestino)) {
        $dica = if ($remoto) {
            "Verifique se o servidor esta no ar, se o compartilhamento existe e se sua conta tem acesso.`n" +
            "Se a conta do Windows local nao for a mesma do servidor, use -Credencial (Get-Credential)."
        } else { "Verifique o caminho." }
        Falhar "Nao alcanco '$paiDoDestino'.`n$dica"
    }

    # =====================================================================
    # PERMISSAO DE ESCRITA
    # =====================================================================
    # Sem isto, a falta de elevacao so aparece no fim, como "ERRO 5 / codigo
    # 16" do robocopy — depois de um build de 20 segundos e sem dizer o que
    # fazer a respeito.
    $pastaParaTestar = $Destino
    if (-not (Test-Path $Destino)) { $pastaParaTestar = $paiDoDestino }

    if (-not (PodeEscrever $pastaParaTestar)) {
        if (-not $remoto -and -not (EstouElevado)) {
            Falhar ("Sem permissao de escrita em '$pastaParaTestar'.`n" +
                    "Esta sessao do PowerShell NAO esta elevada, e C:\inetpub exige Administrador.`n`n" +
                    "  Abra o PowerShell como Administrador e repita o comando.`n" +
                    "  Ou, para nao precisar elevar toda vez, libere a pasta uma unica vez:`n" +
                    "    icacls `"$pastaParaTestar`" /grant `"$env:USERNAME`":(OI)(CI)M")
        }
        Falhar ("Sem permissao de escrita em '$pastaParaTestar'.`n" +
                "Verifique as permissoes da pasta (e do compartilhamento, se remoto).")
    }

    # =====================================================================
    # ROLLBACK
    # =====================================================================
    if ($Reverter) {
        if (-not (Test-Path $PastaBackup)) { Falhar "Nao ha pasta de backups em '$PastaBackup'." }

        $ultimo = Get-ChildItem -LiteralPath $PastaBackup -Directory |
            Sort-Object Name -Descending | Select-Object -First 1
        if (-not $ultimo) { Falhar "Nenhum backup encontrado em '$PastaBackup'." }

        Escrever "Restaurando '$($ultimo.Name)' -> '$Destino'..." 'Yellow'
        Espelhar $ultimo.FullName $Destino
        Escrever "Revertido para $($ultimo.Name)." 'Green'
        exit 0
    }

    # =====================================================================
    # VALIDACAO DO DESTINO
    # =====================================================================
    # `/MIR` APAGA tudo que estiver no destino e nao estiver na origem. Um erro
    # de digitacao no caminho — ou o default apontando para a pasta errada —
    # viraria exclusao silenciosa de dados alheios. Por isso so seguimos quando
    # o destino e reconhecivelmente um deploy deste SPA (ja tem index.html),
    # esta vazio, ou ainda nao existe.
    if (-not $Forcar -and (Test-Path $Destino)) {
        $temIndex = Test-Path (Join-Path $Destino 'index.html')
        $conteudo = @(Get-ChildItem -LiteralPath $Destino -Force -ErrorAction SilentlyContinue)
        $vazia    = $conteudo.Count -eq 0

        # wwwroot recem-instalado so tem a pagina de boas-vindas do IIS.
        # Sobrescrever isso e exatamente a intencao de quem publica ali, e
        # obrigar `-Forcar` seria pior: a flag desliga a checagem INTEIRA.
        $soPaginaPadraoIis = -not $vazia -and -not ($conteudo | Where-Object { $_.Name -notmatch '^iisstart\.(htm|png)$' })

        if (-not $temIndex -and -not $vazia -and -not $soPaginaPadraoIis) {

            # Erro classico: copiar a PASTA `browser` em vez do conteudo dela.
            # O site fica em /browser/, mas o index.html tem `<base href="/">`
            # e pede os arquivos a partir da raiz — da tela branca com 404 no
            # console, e o IIS parece estar funcionando.
            if (Test-Path (Join-Path $Destino 'browser\index.html')) {
                Falhar ("'$Destino' tem uma subpasta 'browser' com o app dentro.`n" +
                        "Isso e a pasta do build copiada inteira; o que vai para a raiz do site e o`n" +
                        "CONTEUDO dela — senao o app abre em branco, porque o index.html referencia`n" +
                        "os arquivos a partir de '/'.`n`n" +
                        "  Apague '$Destino\browser' e rode este script, que ele publica certo.")
            }

            Falhar ("'$Destino' nao esta vazia e nao parece um deploy do SPA (sem index.html na raiz).`n" +
                    "A publicacao APAGARIA o conteudo atual. Confira o caminho.`n" +
                    "Se estiver certo mesmo, repita com -Forcar.")
        }
    }

    # Destino remoto e, na pratica, producao. Uma confirmacao explicita custa
    # dois segundos e evita publicar da branch errada sem perceber.
    if ($remoto -and -not $SemConfirmar) {
        $branch = (& git -C $raizProjeto rev-parse --abbrev-ref HEAD 2>$null)
        $sujo   = (& git -C $raizProjeto status --porcelain 2>$null)

        Write-Host "`n  Destino  : $Destino"  -ForegroundColor Yellow
        Write-Host "  Base href: $BaseHref" -ForegroundColor Yellow
        Write-Host "  Branch   : $branch"   -ForegroundColor Yellow
        if ($sujo) {
            Write-Host "  ATENCAO : ha alteracoes nao commitadas — elas VAO junto." -ForegroundColor Red
        }
        $resposta = Read-Host "`n  Publicar em producao? (digite SIM)"
        if ($resposta -ne 'SIM') { Falhar 'Cancelado por voce.' }
    }

    } # fim do bloco exclusivo da publicacao direta

    # =====================================================================
    # BUILD (vale para os dois modos)
    # =====================================================================
    if ($ComFramework) {
        if (-not $raizFw) {
            throw 'Nao encontrei a pasta atlas-framework subindo a partir de ' +
                  "$raizProjeto. Publique sem -ComFramework, ou compile o framework a mao."
        }
        Escrever "Compilando @atlas/framework ($raizFw)..."
        Push-Location $raizFw
        try {
            & npm run build
            if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o framework.' }
        } finally { Pop-Location }
    }

    # VERSAO antes do build, nunca depois: o numero precisa estar no
    # package.json quando o `prebuild` gera o carimbo, senao o pacote sai com
    # a versao anterior e ninguem percebe.
    #
    # Só o PATCH sobe sozinho. Minor e major dizem respeito ao que mudou para
    # quem usa o sistema, e isso nenhum script sabe olhando o diff — quem
    # decide e quem publica, passando -Versao minor ou -Versao major.
    if ($Versao -ne 'nenhuma') {
        Escrever "Incrementando a versao ($Versao)..."
        Push-Location $raizProjeto
        try {
            & npm version $Versao --no-git-tag-version | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Falha ao incrementar a versao.' }
        } finally { Pop-Location }
    }

    $versaoAtual = (Get-Content (Join-Path $raizProjeto 'package.json') -Raw |
                    ConvertFrom-Json).version
    Escrever "Versao do pacote: $versaoAtual"

    Escrever "Compilando o SPA (producao, base href '$BaseHref')..."
    Push-Location $raizProjeto
    try {
        & npm run build -- --configuration production --base-href $BaseHref
        if ($LASTEXITCODE -ne 0) { throw 'Falha no build do SPA. Nada foi publicado.' }
    } finally { Pop-Location }

    if (-not (Test-Path (Join-Path $origem 'index.html'))) {
        throw "Build terminou sem index.html em '$origem'. Nada foi publicado."
    }

    # =====================================================================
    # MODO PACOTE
    # =====================================================================
    # Gera um ZIP para levar ao servidor a mao, com o app dentro de `app\` e um
    # instalador do lado de la. O instalador existe para o destino tambem ser um
    # comando so: ele ESPELHA (nao sobrescreve) e guarda a versao anterior.
    # Sobrescrever deixaria os chunks antigos acumulando para sempre, porque
    # cada build gera nomes novos com hash.
    if ($Empacotar) {
        # A VERSAO vai no nome, antes do carimbo. Quem recebe o arquivo precisa
        # saber o que esta instalando sem abrir o ZIP, e um nome so com data
        # nao diz — duas builds do mesmo dia ficavam indistinguiveis.
        #
        # O carimbo CONTINUA: duas geracoes da mesma versao (um rebuild depois
        # de corrigir o base href, por exemplo) colidiriam no mesmo nome, e o
        # Compress-Archive logo abaixo sobrescreve sem perguntar.
        $carimbo = Get-Date -Format 'yyyyMMdd-HHmmss'
        $stage   = Join-Path $raizProjeto "dist\pacote-$carimbo"
        $zip     = Join-Path $raizProjeto "dist\midia-indoor-web-v$versaoAtual-$carimbo.zip"

        New-Item -ItemType Directory -Force (Join-Path $stage 'app') | Out-Null
        Espelhar $origem (Join-Path $stage 'app')

        $instalador = @'
@echo off
setlocal
set DESTINO=%~1

if "%DESTINO%"=="" (
  echo.
  echo Uso: instalar-no-servidor.cmd "C:\caminho\da\pasta\do\site"
  echo.
  exit /b 1
)

if not exist "%DESTINO%" (
  echo ABORTADO: a pasta "%DESTINO%" nao existe.
  exit /b 1
)

set CARIMBO=%RANDOM%
set BACKUP=%DESTINO%\..\_backup-site-%CARIMBO%

echo.
echo Guardando a versao atual em "%BACKUP%" ...
robocopy "%DESTINO%" "%BACKUP%" /MIR /R:1 /W:1 /NFL /NDL /NP >nul

echo Espelhando a versao nova ...
rem /XF config.json: o assets\config.json carrega o endereco da API e e
rem EDITADO NESTE SERVIDOR. Sem excluir, o /MIR o substituiria pelo do build
rem e o endereco voltaria ao de desenvolvimento a cada publicacao.
robocopy "%~dp0app" "%DESTINO%" /MIR /R:2 /W:2 /NFL /NDL /NP /XF config.json

rem Robocopy usa 0..7 para resultados normais (1=copiou, 2=removeu extras).
rem Só de 8 para cima e falha de verdade.
if %ERRORLEVEL% GEQ 8 (
  echo.
  echo FALHOU. Verifique permissao e arquivos em uso.
  exit /b 1
)

rem Primeira instalacao: o arquivo ainda nao existe no destino, e o /XF acima
rem impediu a copia. Sem isto o app subiria sem configuracao nenhuma.
if not exist "%DESTINO%\assets\config.json" (
  echo Criando assets\config.json ^(primeira instalacao^) ...
  copy /y "%~dp0app\assets\config.json" "%DESTINO%\assets\" >nul
  echo AJUSTE O ENDERECO DA API em "%DESTINO%\assets\config.json"
)

echo.
echo Publicado em "%DESTINO%".
echo Backup da versao anterior: "%BACKUP%"
echo Se o navegador mostrar a versao antiga, use Ctrl+F5.
exit /b 0
'@
        $instalador | Set-Content (Join-Path $stage 'instalar-no-servidor.cmd') -Encoding Ascii

        $leiame = @"
PACOTE DO FRONT - Mídia Indoor

Versao ........: $versaoAtual
Gerado em .....: $(Get-Date -Format 'dd/MM/yyyy HH:mm')
Base href .....: $BaseHref

Depois de instalar, confira em /versao.json que o site responde esta versao.
Se responder outra, o pacote instalado nao foi este.

O <base href> acima e o caminho pela URL, NAO o caminho em disco. Se o site do
IIS responde na raiz, ele precisa ser "/"; numa subpasta /midia-indoor-web, "/midia-indoor-web/". Se
estiver errado, o app abre em TELA BRANCA com 404 nos chunks.

COMO INSTALAR (no servidor, prompt como Administrador):

  instalar-no-servidor.cmd "C:\inetpub\wwwroot\SUA_PASTA"

O instalador espelha a pasta e guarda a versao anterior ao lado, em
_backup-site-*.

ENDERECO DA API: fica em assets\config.json, DENTRO da pasta do site. E um
arquivo texto — editar e recarregar a pagina basta, sem recompilar nada. O
instalador NAO sobrescreve o que ja existe no servidor; so cria na primeira
instalacao.

PRE-REQUISITO no servidor: modulo URL Rewrite do IIS instalado.
Sem ele o site responde HTTP 500.19.
"@
        $leiame | Set-Content (Join-Path $stage 'LEIA-ME.txt') -Encoding UTF8

        if (Test-Path $zip) { Remove-Item $zip -Force }
        Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
        Remove-Item $stage -Recurse -Force

        Escrever "Pacote pronto: $zip" 'Green'
        Write-Host "   base href gravado: $BaseHref" -ForegroundColor DarkGray
        Write-Host "   No servidor: extraia e rode instalar-no-servidor.cmd `"<pasta do site>`"" -ForegroundColor DarkGray
        exit 0
    }

    # =====================================================================
    # BACKUP DA VERSAO NO AR
    # =====================================================================
    # Depois do build, de proposito: build quebrado nao chega aqui, e assim
    # nao se gasta backup nem se mexe no destino a toa.
    if ($ManterBackups -gt 0 -and (Test-Path (Join-Path $Destino 'index.html'))) {
        $carimbo = Get-Date -Format 'yyyyMMdd-HHmmss'
        $alvo    = Join-Path $PastaBackup $carimbo

        Escrever "Guardando a versao atual em '$alvo'..."
        New-Item -ItemType Directory -Force -Path $alvo | Out-Null
        Espelhar $Destino $alvo

        # Nomes sao carimbos de data, entao ordem alfabetica = ordem cronologica.
        Get-ChildItem -LiteralPath $PastaBackup -Directory |
            Sort-Object Name -Descending |
            Select-Object -Skip $ManterBackups |
            ForEach-Object {
                Write-Host "   removendo backup antigo: $($_.Name)" -ForegroundColor DarkGray
                Remove-Item -LiteralPath $_.FullName -Recurse -Force
            }
    }

    # =====================================================================
    # PUBLICACAO
    # =====================================================================
    # `assets/config.json` carrega o endereco da API e e EDITADO NO SERVIDOR.
    # O espelhamento o substituiria pelo do build, e o endereco voltaria ao de
    # desenvolvimento a cada publicacao — em silencio. Guarda antes, repoe
    # depois.
    $caminhoConfig = Join-Path $Destino 'assets\config.json'
    $configPreservado = $null
    if (Test-Path $caminhoConfig) {
        $configPreservado = Get-Content $caminhoConfig -Raw
    }

    Escrever "Espelhando '$origem' -> '$Destino'..."
    Espelhar $origem $Destino

    if ($configPreservado) {
        Set-Content $caminhoConfig -Value $configPreservado -NoNewline -Encoding UTF8
        Write-Host "   assets/config.json do servidor preservado" -ForegroundColor DarkGray
    }

    Escrever "Publicado em '$Destino' ($(TamanhoDe $Destino))." 'Green'
    if ($ManterBackups -gt 0) {
        Write-Host "   Para voltar atras: .\scripts\publicar-iis.ps1 -Destino `"$Destino`" -Reverter" -ForegroundColor DarkGray
    }
    if ($BaseHref -ne '/') {
        Write-Host "   O app responde em $BaseHref — e precisa ser uma APLICACAO no IIS, nao so uma pasta." -ForegroundColor DarkGray
    }
    Write-Host '   Se o IIS servir versao antiga, e cache do NAVEGADOR: Ctrl+F5.' -ForegroundColor DarkGray

    Write-Host "   Versao publicada: $versaoAtual  (confira em /versao.json)" -ForegroundColor Cyan
    Write-Host "   Para marcar no git: git tag v$versaoAtual" -ForegroundColor DarkGray

} finally {
    if ($driveTemporario) { Remove-PSDrive -Name 'PubMidiaIndoor' -Force -ErrorAction SilentlyContinue }
}

exit 0

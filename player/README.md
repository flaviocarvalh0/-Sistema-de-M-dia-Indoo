# Mídia Indoor — player das TVs

App Flutter para Android TV (e TV box Android). Pareia com um código gerado
no painel, baixa as mídias da grade e toca em loop **só a partir de arquivos
locais**: sem internet, continua tocando o que já baixou. O contrato com a API
está em [`docs/CONTRATO_DISPOSITIVO.md`](../docs/CONTRATO_DISPOSITIVO.md).

## Pré-requisitos

- Flutter estável atual (desenvolvido com 3.47 / Dart 3.13) — `flutter --version`.
- Android SDK (Android Studio) e um emulador **Android TV** ou uma TV/TV box
  com depuração USB/ADB ligada. Android 7.0 (API 24) ou mais novo.

## Rodar

```powershell
cd player
flutter pub get
flutter run --dart-define=API_URL=https://10.0.2.2:7216
```

- `10.0.2.2` é o "localhost" do PC visto de dentro do emulador; `7216` é a
  porta https da API (perfil `https`). Use https: a API redireciona http para
  https.
- Em debug/profile o app aceita o certificado de desenvolvimento do .NET (que
  não vale na TV); o APK de **release** exige um certificado válido.
- Numa TV ou celular de verdade use o IP do PC na rede (`ipconfig` →
  "Endereço IPv4" do Wi-Fi/Ethernet, ex.: `https://192.168.1.11:7216`) e suba
  a API ouvindo na rede, pela variável de ambiente e sem o perfil de
  inicialização (o perfil fixa `localhost` e passa por cima do `--urls`):

  ```powershell
  $env:ASPNETCORE_ENVIRONMENT = "Development"
  $env:ASPNETCORE_URLS = "https://*:7216;http://*:5366"
  dotnet run --project src/Atlas.Web --no-launch-profile
  ```

  O log deve mostrar `Now listening on: https://[::]:7216`. Use `*`, não
  `0.0.0.0` (só IPv4: no Windows o `localhost` do painel tenta IPv6 primeiro e
  cada requisição espera ~2 s). Libere a porta no firewall (PowerShell como
  administrador):
  `New-NetFirewallRule -DisplayName "Midia Indoor API" -Direction Inbound -Protocol TCP -LocalPort 7216,5366 -Action Allow -Profile Any`.
  Teste no navegador do aparelho: `https://<IP do PC>:7216/scalar`
  ("conexão recusada" = API presa ao localhost; "tempo esgotado" = firewall
  ou rede diferente).
- O endereço também pode ser trocado na própria TV, na tela de pareamento
  ("Servidor: … · alterar"); fica gravado.

Para gerar o instalável, use o script, que já sobe a versão do
`pubspec.yaml` (o código depois do `+`, que a TV compara para se atualizar) e
põe o APK em `player/dist/` com a versão no nome:

```powershell
cd player
.\scripts\gerar-apk.ps1                         # Profile, rede local: 1.0.0+1 -> 1.0.1+2
.\scripts\gerar-apk.ps1 -Modo Release -ApiUrl https://api.suaempresa.com.br
.\scripts\gerar-apk.ps1 -Modo Debug -Instalar -Aparelho 192.168.1.50:5555
Get-Help .\scripts\gerar-apk.ps1 -Detailed       # todas as opções
```

- **Profile** (padrão): para testar na rede local. Aceita o certificado de
  desenvolvimento (`https://<IP do PC>:7216`, detectado sozinho).
- **Release**: produção. Só aceita certificado HTTPS válido (domínio ou túnel);
  o script recusa IP e `localhost`.
- **Debug**: depuração; o APK costuma passar dos 50 MB de Versões do app.
- Gera **um APK por arquitetura** (o universal passa dos 50 MB de Versões do
  app) e, no fim, mostra o que preencher na página: o arquivo (`armeabi-v7a`,
  que roda em TV de 32 e de 64 bits), o código, o nome e `-Notas` (O que mudou).
- `-Subir Patch|Minor|Major|Codigo|Nenhum` (padrão `Patch`), `-Universal` (um APK
  só, para instalar à mão), `-Instalar` (adb, escolhe o APK da arquitetura da
  TV). Se o build falhar, o `pubspec.yaml` volta.
- Bloqueado pela política de execução do Windows? Rode
  `powershell -ExecutionPolicy Bypass -File .\scripts\gerar-apk.ps1`.

Na mão, o equivalente é
`flutter build apk --release --dart-define=API_URL=https://api.suaempresa.com.br`
(`build/app/outputs/flutter-apk/app-release.apk`), subindo o `version` antes.

## Uso na TV

1. No painel, a operadora cadastra a TV em **Rede → Telas → Novo** (escolhe o
   estabelecimento e a playlist) e usa **Gerar código**.
2. Na TV, abra **Mídia Indoor**, digite o código (aceita minúsculas e hífen)
   e **Parear**.
3. A TV baixa a grade e começa a tocar. No painel, a tela fica **Online**.

Com o controle: **OK** (ou Menu/Info) mostra o painel da TV — tela, conexão,
última sincronização, itens de hoje, notícias, clima, servidor e versão — com
o botão **Parear de novo**.

### Abrir sozinho ao ligar a TV

O app escuta o `BOOT_COMPLETED`. No Android 10 ou mais novo, o sistema só
deixa abrir a tela a partir do boot se o app puder **sobrepor a outros
apps**: em *Configurações → Apps → Mídia Indoor → Permissões* (ou *Acesso
especial → Sobrepor a outros apps*), libere uma vez. Em TV box antiga
(Android 7–9) funciona sem isso.

## Como funciona

```text
lib/
  main.dart                       tela cheia, paisagem, tela sempre acesa
  app.dart                        dependências; decide entre pareamento e reprodução
  config/configuracao.dart        API_URL (--dart-define)
  config/versao_app.dart          nome e código da versão, lidos do APK (package_info_plus)
  dados/
    api_dispositivo.dart          POST /autenticar e GET /sincronizacao (dio), erros tipados
    modelos.dart                  Pareamento, RespostaSincronizacao, ItemGrade, Composicao, Noticia, Clima
    banco_local.dart              grade (com o QR code), notícias, composição, clima e estado (sqflite, versão 3)
    credenciais.dart              token, id do aparelho e servidor (flutter_secure_storage)
  sincronizacao/
    baixador_midia.dart           .part → SHA-256 em blocos → renomeia; 3 tentativas (mídias e imagens das notícias)
    sincronizador.dart            um ciclo: pergunta, baixa, troca grade e notícias de uma vez, limpa
    atualizador_app.dart          baixa a versão nova do app e chama o instalador nativo (ADR-021)
    registrador_exibicoes.dart    grava cada anúncio exibido e envia em lotes (prova de veiculação, ADR-023)
  reproducao/filtro_do_dia.dart   "hoje" no fuso do parceiro (local da TV), dias da semana e período
  reproducao/composicao_fila.dart          a fila de cada volta: embaralhar (fixos no lugar) e notícia a cada N
  reproducao/controlador_reproducao.dart   o loop sem tela: próximo passo da fila, fim, falha, espera
  reproducao/video_reproducao.dart         vídeo (video_player) atrás de uma interface testável
  telas/
    pareamento_page.dart          digitação do código
    reproducao_page.dart          loop de vídeo/imagem/notícia, espera, painel de informações
    noticia_view.dart             notícia em tela cheia: foto, título animado e crédito
    sobreposicao_relogio_clima.dart   relógio (fuso do estabelecimento) e clima no canto
    cartoes_view.dart             cartões "Previsão do tempo" e "Cotações" (dólar e euro)
    qr_code_sobreposto.dart       QR code do anúncio (ou o do diretório da TV) por cima da mídia (qr_flutter, gerado na TV)
    painel_info.dart              painel de informações (botão OK do controle)
```

- **Arquivos pelo hash:** cada mídia vira `midias/<sha256>.<ext>`. A mesma
  mídia em grades diferentes é um arquivo só, e um arquivo final só existe
  depois de conferido.
- **Troca atômica:** a grade nova fica em memória enquanto baixa; só quando
  todos os arquivos estão prontos ela entra no banco (uma transação) e os
  arquivos que saíram são apagados. Falhou um download? A grade antiga
  continua e o próximo ciclo vem em 1 minuto, já com URLs novas.
- **Sem internet:** o filtro do dia roda no aparelho a cada item, no fuso do
  parceiro, então a virada do dia e o período de exibição valem offline.
- **401** (TV desvinculada): apaga o token, continua tocando e mostra
  "TV não pareada · pressione OK para parear". **403**, 429, 5xx e sem rede:
  continua tocando e tenta no próximo ciclo.
- **Loop:** vídeo avança no fim (com `duracaoSegundos` + 5 s de limite de
  segurança), imagem fica `duracaoSegundos`; arquivo que não abre é pulado.
  Cada item ganha uma *geração*: eventos atrasados ou repetidos de um item
  que já acabou (o fim de vídeo avisado duas vezes, o timer de segurança) são
  ignorados, então o loop nunca avança duas vezes nem abre o mesmo vídeo de
  novo.
- **Grade nova sem cortar:** o que está na tela vai até o fim e o próximo
  item já sai da grade nova. A TV pergunta ao servidor a cada 60 s.
- **Fila montada na TV:** a cada volta, se a playlist pedir, a ordem dos
  anúncios é sorteada (os itens com 📌 ficam no lugar) e entra uma notícia a
  cada N anúncios. Por ser na TV, cada volta sai diferente mesmo offline.
- **Notícias:** as imagens ficam em `noticias/<sha256>.<ext>`, separadas das
  mídias; a lista nova só entra com as imagens prontas. Notícia com mais de
  48 h sai da tela, mesmo sem internet. O crédito da fonte aparece sempre.
- **Cartões:** "Previsão do tempo" e "Cotações" entram no rodízio dos
  informativos (notícia, tempo, notícia, cotações), com o último dado
  recebido; sem dado válido, saem do rodízio.
- **Relógio e clima:** no canto, por cima de tudo. A hora é a do fuso do
  estabelecimento; o clima guardado vale até 6 h (com o crédito do
  Open-Meteo).
- **Prova de veiculação:** cada anúncio que sai da tela vira uma linha na
  tabela `exibicao` (mídia, início, duração, se foi até o fim, se a TV estava
  sem conexão), com um UUID. Depois de uma sincronização que respondeu, as
  pendentes sobem em lotes de 500 (`POST /exibicoes`), no máximo a cada
  15 min. Sem internet ficam guardadas (até 20 mil).
- **Atualização automática:** a TV manda o código da versão (o `+N` do
  pubspec) e, quando o servidor libera uma maior, baixa o APK em `app/`
  (hash conferido) e entrega ao `PackageInstaller` (`android/.../Instalador.kt`,
  canal `br.com.midiaindoor/atualizador`). Com o app como device owner
  (`AdministradorAparelho`) instala sem perguntar; senão o
  `ReceptorInstalacao` abre a confirmação do Android na TV. Depois o
  `ReceptorInicializacao` reabre o app. Falhou? Tenta de novo em 1 h.
  Configuração da TV e publicação: `docs/IMPLANTACAO.md`, 4.2 e 4.3.

## Testes

```powershell
flutter analyze
flutter test
```

Os testes do sincronizador e do download sobem uma API e um storage falsos
num `HttpServer` local e usam SQLite de verdade (`sqflite_common_ffi`): grade
nova, reaproveitamento de arquivos, hash errado, URL vencida, 401/403, sem
rede, renovação de token e pareamento.

Teste de ponta a ponta contra a API rodando (pareia, sincroniza e baixa do
storage):

```powershell
$env:INDOOR_API_URL = "https://localhost:7216"
$env:INDOOR_CODIGO = "<código gerado no painel>"
flutter test test/integracao_api_test.dart
```

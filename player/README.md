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

Para gerar o instalável:

```powershell
flutter build apk --release --dart-define=API_URL=https://api.suaempresa.com.br
# build/app/outputs/flutter-apk/app-release.apk  →  adb install -r <apk>
```

## Uso na TV

1. No painel, a operadora cadastra a TV em **Rede → Telas → Novo** (escolhe o
   parceiro) e usa **Gerar código**. O parceiro escolhe a playlist em
   **Mídia Indoor → Telas → Trocar playlist**.
2. Na TV, abra **Mídia Indoor**, digite o código (aceita minúsculas e hífen)
   e **Parear**.
3. A TV baixa a grade e começa a tocar. No painel, a tela fica **Online**.

Com o controle: **OK** (ou Menu/Info) mostra o painel da TV — tela, conexão,
última sincronização, itens de hoje, servidor e versão — com o botão
**Parear de novo**.

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
  config/configuracao.dart        API_URL e versão (--dart-define)
  dados/
    api_dispositivo.dart          POST /autenticar e GET /sincronizacao (dio), erros tipados
    modelos.dart                  Pareamento, RespostaSincronizacao, ItemGrade
    banco_local.dart              grade em uso e estado (sqflite)
    credenciais.dart              token, id do aparelho e servidor (flutter_secure_storage)
  sincronizacao/
    baixador_midia.dart           .part → SHA-256 em blocos → renomeia; 3 tentativas
    sincronizador.dart            um ciclo: pergunta, baixa, troca a grade de uma vez, limpa
  reproducao/filtro_do_dia.dart   "hoje" no fuso do parceiro, dias da semana e período
  telas/
    pareamento_page.dart          digitação do código
    reproducao_page.dart          loop de vídeo/imagem, espera, painel de informações
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
Supabase):

```powershell
$env:INDOOR_API_URL = "https://localhost:7216"
$env:INDOOR_CODIGO = "<código gerado no painel>"
flutter test test/integracao_api_test.dart
```

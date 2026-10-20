# Como testar na sua máquina

A API sobe primeiro (passos 1 a 5); depois o painel `web/` (passo 7) e o app
das TVs `player/` (passo 8). Sem TV, o papel dela também pode ser feito pelo
roteiro `api/src/Atlas.Web/Indoor.http`.

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
# Storage: Cloudflare R2 (ADR-025). Token só com "Object Read & Write" no bucket.
dotnet user-secrets set "Indoor:Armazenamento:R2:Conta" "<Account ID>" --project src/Atlas.Web
dotnet user-secrets set "Indoor:Armazenamento:R2:ChaveAcesso" "<Access Key ID>" --project src/Atlas.Web
dotnet user-secrets set "Indoor:Armazenamento:R2:SegredoAcesso" "<Secret Access Key>" --project src/Atlas.Web
```

Ainda no Supabase? Use `Indoor:Armazenamento:Supabase:Url` e
`Indoor:Armazenamento:Supabase:ChaveServico` no lugar das três do R2. Com os
dois e `Indoor:Armazenamento:CopiarDoSupabase` = `true`, a subida copia tudo
para o R2 (ver `IMPLANTACAO.md`, 2.7).

No R2, libere o CORS do bucket para `http://localhost:4200` (a API tenta na
subida; se o token não puder, o log avisa e mostra como fazer no painel da
Cloudflare).

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

Com a API já tendo subido uma vez, aplique o script do Studio: módulos
(launcher), entidades com rota e ícone, menus, grupos e RBAC. Ele pode ser
rodado de novo a qualquer momento — só cria o que falta e corrige o que
divergir — e termina listando as rotas e as permissões de cada grupo.

```powershell
.\docs\scripts\aplicar-script.ps1 .\docs\scripts\studio-indoor.sql   # pede a senha; ajuste -Porta/-Psql se preciso
# ou
psql -h localhost -U postgres -d Indoor -v ON_ERROR_STOP=1 -f docs/scripts/studio-indoor.sql
```

Depois saia e entre de novo no painel: as permissões e os menus são lidos
no login.

## 6. Testar

**Pelo roteiro (recomendado):** abra `api/src/Atlas.Web/Indoor.http`,
coloque um `video.mp4` e uma `imagem.png` na mesma pasta e execute as
requisições de cima para baixo. O roteiro cadastra um cliente (anunciante), entra como o
dono, envia vídeo e imagem ao Supabase e, de volta como operadora, monta a
playlist com as mídias dele, cria a tela com a playlist e gera o código de
pareamento. Os passos 12 a 15 fazem o papel da TV: pareiam com o
código, sincronizam (as `url` da resposta abrem o arquivo) e mostram a tela
Online no painel. O contrato completo está em [CONTRATO_DISPOSITIVO.md](CONTRATO_DISPOSITIVO.md).

**Pela documentação interativa:** `https://localhost:7216/scalar`. Faça o
login em `POST /api/Auth/login` com `"mode": "Cookie"`; as demais chamadas
usam o cookie.

### O que conferir

- `GET /midias/{id}` traz `urlVisualizacao`: abra no navegador e o arquivo
  toca (vem do Supabase, válida por 1 h).
- Enviar um PNG como `video/mp4` é recusado e o arquivo some do bucket.
- Enviar além da cota do cliente é recusado antes do upload.
- Excluir uma mídia que está numa playlist, ou uma playlist que está numa
  tela, é recusado.
- Com o plano padrão (1 mídia ativa), a segunda mídia nasce inativa e
  ativá-la dá 400 com a mensagem do plano. Reduzir o plano não inativa nada.
- Uma mídia inativa não entra numa grade (400); a mesma mídia ativa pode
  entrar duas vezes.
- `PUT /midias/{id}/arquivo` troca o arquivo: a TV recebe o hash novo na
  próxima sincronização e o arquivo antigo some do bucket.
- O cliente recebe 403 em `rede/parceiros`, `rede/clientes`, `rede/playlists`, `rede/midias` e `rede/fontes-noticia`.
- `POST rede/fontes-noticia/{id}/coletar` devolve quantas notícias entraram
  (ou o erro do feed); `GET rede/fontes-noticia/{id}/noticias` mostra as
  notícias com a imagem guardada.
- Com "notícia a cada N" na playlist, a sincronização da TV traz `noticias`;
  mandando a `versaoNoticias` recebida, `noticias` volta nulo (e a grade de
  anúncios não é reenviada quando só as notícias mudam).
- Com a cidade do parceiro (local da TV) preenchida (ex.: "Manaus" ou "São José - SC"), em até
  30 min a sincronização traz `clima`.
- Salvar com uma `version` antiga devolve 409.
- `POST /dispositivo/exibicoes` com o token da TV grava o lote; reenviar o mesmo
  lote responde `jaRecebidas` e não duplica. Mídia desconhecida conta em
  `descartadas`.
- Com o parâmetro `INDOOR.URL_PUBLICA` (ou `Indoor:UrlPublica`) configurado, uma mídia com QR de WhatsApp ganha
  `codigoQr`; `GET /q/{codigo}` responde 302 para o `wa.me` e grava uma linha em
  `indoor.leitura_qr` (sem IP).
- O `admin` não enxerga o painel do cliente (403): ele não pertence a um cliente.
- Desvincular a TV no painel faz a sincronização dela responder 401; inativar
  a tela, 403. Bloquear o cliente tira as mídias dele da grade; a TV continua.

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

E contra o Cloudflare R2 (use um bucket de testes, `midias-testes` por padrão;
confere também que o R2 recusa arquivo maior que o declarado):

```powershell
$env:INDOOR_R2_CONTA = "<Account ID>"
$env:INDOOR_R2_CHAVE = "<Access Key ID>"
$env:INDOOR_R2_SEGREDO = "<Secret Access Key>"
dotnet test tests/Atlas.Infrastructure.IntegrationTests --filter "FullyQualifiedName~ArmazenamentoR2Contrato"
```

## 7. Painel (`web/`)

Pré-requisitos: Node.js 20+ e o `atlas-framework` clonado **ao lado** da pasta
deste repositório, com o `dist` gerado:

```powershell
cd ..\..            # a pasta que contém -Sistema-de-M-dia-Indoo
git clone <url do atlas-framework> atlas-framework
cd atlas-framework
npm install
npm run build
```

Com a API no ar (passo 5):

```powershell
cd -Sistema-de-M-dia-Indoo\web
npm install
npm start
```

Abra `http://localhost:4200` (a porta tem de ser essa: é a liberada no CORS
da API).

### Roteiro

1. Entre como `admin` e abra **Rede → Clientes**. Cadastre um cliente (quem
   anuncia): o responsável recebe a senha temporária por e-mail (sem SMTP configurado,
   consulte `select html_body from platform.email_message order by id desc limit 1;`).
2. Saia e entre com o e-mail do responsável; troque a senha.
3. **Mídias → Enviar mídia**: escolha um MP4 (a duração vem do vídeo). A
   barra mostra o envio direto ao Supabase; ao terminar a mídia aparece na
   lista, a barra de espaço cresce e o plano mostra **Mídias ativas: 1 de 1**.
   Envie uma imagem: com o plano completo, a página avisa e ela nasce
   inativa. Use o play da linha para pré-visualizar.
4. Na linha do vídeo, **Substituir arquivo**: escolha outro MP4. A mídia
   continua ativa e nas mesmas grades; as TVs baixam o arquivo novo.
5. Saia e entre como `admin` (ou um usuário do grupo **Operadora**).
   **Rede → Playlists → Novo**, depois clique na playlist para abrir a grade:
   busque as mídias ativas da rede (por título ou cliente), adicione — a
   mesma mídia pode entrar mais de uma vez e aparece "Já adicionado (N×)" —,
   arraste para mudar a ordem, desmarque dias e **Salvar grade**.
6. **Rede → Parceiros → Novo**: cadastre o local onde a TV fica (cidade e
   fuso; se o dono também anuncia, escolha o cliente em **Também anuncia
   como**). Depois **Rede → Telas → Novo**: escolha o parceiro (que define o
   fuso), a playlist do passo 5, o nome e, se quiser, a identificação do
   aparelho. Use o botão de QR Code da linha (**Gerar código**): o código
   aparece em destaque, com o tempo restante.
7. Faça o papel da TV com os passos do `Indoor.http` usando esse código: a
   tela passa a **Online** (a lista se atualiza a cada minuto).
8. Entre de novo como o dono: em **Onde meus anúncios passam** a TV aparece
   somente leitura, com o parceiro (local) e a conexão.
9. Como operadora, em **Rede → Clientes**, a coluna **Mídias ativas** mostra
   o uso do plano; edite o cliente e aumente **Mídias ativas (plano)** para
   o dono conseguir ativar a imagem.
10. **Desvincular TV** na linha da tela: a próxima sincronização da TV recebe
    401 e a tela volta a *Aguardando pareamento*. Trocar o parceiro ou
    a playlist da tela não desvincula a TV.
11. **Rede → Fontes de notícias:** a Agência Brasil já vem cadastrada. Use
    **Coletar agora** (ícone de setas) e clique na linha para ver as notícias
    com as fotos. Sem internet na máquina da API, a fonte aparece "Com erro"
    com a mensagem.
12. **Rede → Playlists → editar:** "Um informativo a cada" 2, marque
    **Embaralhar**, **Cartão do tempo** e **Cartão de cotações**; deixe
    **Relógio** e **Clima** marcados. Na grade, clique
    no 📌 do primeiro item para ele não entrar no sorteio e salve. Na TV, em
    até 1 minuto a fila passa a intercalar notícias, com a hora e a
    temperatura no canto (o clima precisa da cidade preenchida no cadastro do
    parceiro).
13. **QR code:** como dono, edite uma mídia e abra a aba **QR code**. Escolha
    **WhatsApp**, informe o telefone com DDD e uma mensagem e salve. A coluna
    **QR code** mostra o tipo; o ícone de QR da linha (**Ver QR code**) abre a
    prévia — aponte a câmera do celular para conferir. Teste também o
    **Pix** (chave, nome e cidade; valor opcional): uma chave inválida ou um
    campo faltando mostra o erro sem salvar. Na TV, o QR aparece por cima do
    anúncio no canto e no tamanho escolhidos.
14. **Diretório da TV:** como dono, em **Mídias**, use **Aparecer no
    diretório** no cartão "Diretório das TVs": marque o aceite e preencha
    WhatsApp, Instagram ou site. Como operadora, edite a tela em **Rede →
    Telas**, marque **Diretório de anunciantes** e salve; o ícone de pessoas
    da linha (**Diretório de anunciantes**) mostra o QR code e o endereço.
    **Abrir página** mostra o que o celular vê: o cliente ligado ao parceiro primeiro e
    só quem aceitou. Para ler com o celular de verdade, a API precisa de um
    endereço que o celular alcance no parâmetro `INDOOR.URL_PUBLICA` (Atlas
    Studio → Parâmetros). `localhost` é ignorado (o celular abriria a si
    mesmo): na rede local use `http://<IP do PC>:5366` com o celular no mesmo
    Wi-Fi, ou o endereço do túnel. Com `INDOOR.ANUNCIE.WHATSAPP` preenchido, a
    TV mostra "Quer anunciar aqui?" no cartão do QR e a página ganha o botão
    **Clique aqui e fale com a gente**, que abre o seu WhatsApp já dizendo de
    qual local veio o interesse.

15. **Carrossel:** em **Rede → Playlists → editar**, ponha **Tamanho da
    volta** = 30 numa grade com 6 imagens de 10 s (e **Embaralhar** ligado).
    Na grade aparece "voltas de 0:30: todos passam a cada 2 voltas". Na TV,
    cada volta mostra 3 anúncios e os 6 passam antes de qualquer um repetir.

## 8. App das TVs (`player/`)

Pré-requisitos: Flutter (estável atual) e Android Studio com um emulador
**Android TV** (Device Manager → *Television*), ou uma TV/TV box Android com
depuração ADB ligada.

```powershell
cd player
flutter pub get
flutter test                                              # testes do app
flutter run --dart-define=API_URL=https://10.0.2.2:7216   # emulador
```

1. No painel, gere o código da tela (passo 7, item 5).
2. Na TV, digite o código e **Parear**. A TV baixa a grade e começa a tocar;
   no painel a tela vira **Online**.
3. Mude a grade no painel (tire um dia, troque a ordem): a TV pega na próxima
   sincronização (até 1 min, mais o tempo de baixar; o vídeo que está passando vai até o fim antes de trocar).
4. Desligue a rede do emulador: a TV continua tocando. **Desvincular TV** no
   painel: a TV continua tocando e mostra "TV não pareada · pressione OK".
5. Botão **OK** do controle (Enter no teclado do emulador) mostra o painel de
   informações da TV.
6. Cadastre um QR code numa mídia (passo 7, item 13): na próxima
   sincronização a TV mostra o QR por cima do anúncio. Leia com o celular a
   uns 2 m da tela; se não ler, use o tamanho **Grande**.
7. Ligue o diretório na tela (passo 7, item 14): nos anúncios sem QR próprio
   aparece um QR pequeno no canto inferior direito, "Contatos dos
   anunciantes". O painel de informações (OK) mostra o endereço.
8. **Atualização automática** (ADR-021), com um aparelho ou emulador onde o
   app foi instalado por APK de release:
   1. Torne o app device owner (`docs/IMPLANTACAO.md`, 4.2, passo 6). No
      emulador sem conta Google o comando funciona direto.
   2. Suba o `+N` do `pubspec.yaml` (ex.: `1.0.0+1` → `1.0.1+2`), gere o APK
      e envie em **Rede → Telas → Versões do app**.
   3. Marque a tela como **TV de teste** e libere **Só TVs de teste**. Em até
      2 min a TV baixa, instala e reabre sozinha; a coluna **App** mostra
      `1.0.1 (2)` e o painel de informações (OK) mostra "em dia".
   4. Sem device owner: a TV mostra a confirmação do Android; aperte
      **Instalar**. Recusou? Ela tenta de novo em 1 h.
9. **Alerta de TV fora do ar** (ADR-020): em **Atlas Studio → Parâmetros**,
   ponha seu e-mail em `INDOOR.ALERTAS.EMAILS` (ex.: `["voce@gmail.com"]`) e,
   para testar rápido, `INDOOR.ALERTAS.TV_OFFLINE_MINUTOS` = 5 (o mínimo); em
   **Atlas Studio → Servidor SMTP**, configure e envie o teste. Feche o app da TV: em até 10 min chega "1 TV(s) fora do ar"
   (5 de tolerância + a varredura, que roda a cada 5 min); abra de novo e chega
   "de volta ao ar". Fora do horário (7 h–22 h no fuso do
   estabelecimento) não há alerta.
10. **Prova de veiculação e QR rastreado** (ADR-023): com `INDOOR.URL_PUBLICA`
    num endereço que o celular alcance (não `localhost`: aí o QR vai direto ao
    destino e a leitura não é contada) e uma mídia com QR de WhatsApp na grade, deixe a TV tocar
    alguns minutos. Em até 15 min (ou na próxima sincronização depois de
    reabrir o app) as exibições sobem. Leia o QR com o celular: abre o WhatsApp
    e conta uma leitura. Em **Mídias → Relatório de veiculação** (dono) ou na
    ação **Relatório de veiculação** da lista de Clientes (operadora) aparecem
    as exibições, o tempo na tela, as leituras e o horário de pico.
    **Copiar resumo para o WhatsApp** põe o texto na área de transferência.
    Desligue a internet da TV: ela continua gravando e, quando voltar, envia
    as exibições marcadas como "sem internet".

TV de verdade na rede: veja `player/README.md` (API ouvindo em `*` — não em `0.0.0.0`, que deixa o painel lento —,
endereço `https://<IP do PC>:7216`).


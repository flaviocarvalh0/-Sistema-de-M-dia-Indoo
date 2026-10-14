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
requisições de cima para baixo. O roteiro cadastra um parceiro, entra como o
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
- Enviar além da cota do parceiro é recusado antes do upload.
- Excluir uma mídia que está numa playlist, ou uma playlist que está numa
  tela, é recusado.
- Com o plano padrão (1 mídia ativa), a segunda mídia nasce inativa e
  ativá-la dá 400 com a mensagem do plano. Reduzir o plano não inativa nada.
- Uma mídia inativa não entra numa grade (400); a mesma mídia ativa pode
  entrar duas vezes.
- `PUT /midias/{id}/arquivo` troca o arquivo: a TV recebe o hash novo na
  próxima sincronização e o arquivo antigo some do bucket.
- O parceiro recebe 403 em `rede/playlists`, `rede/midias` e `rede/fontes-noticia`.
- `POST rede/fontes-noticia/{id}/coletar` devolve quantas notícias entraram
  (ou o erro do feed); `GET rede/fontes-noticia/{id}/noticias` mostra as
  notícias com a imagem guardada.
- Com "notícia a cada N" na playlist, a sincronização da TV traz `noticias`;
  mandando a `versaoNoticias` recebida, `noticias` volta nulo (e a grade de
  anúncios não é reenviada quando só as notícias mudam).
- Com a cidade do host preenchida (ex.: "Manaus" ou "São José - SC"), em até
  30 min a sincronização traz `clima`.
- Salvar com uma `version` antiga devolve 409.
- O `admin` não enxerga o painel de parceiro (403): ele não pertence a um parceiro.
- Desvincular a TV no painel faz a sincronização dela responder 401; inativar
  a tela, 403. Bloquear o anunciante tira as mídias dele da grade; bloquear o
  host não afeta a TV.

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

1. Entre como `admin` e abra **Rede → Parceiros**. Cadastre um parceiro: o
   responsável recebe a senha temporária por e-mail (sem SMTP configurado,
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
   busque as mídias ativas da rede (por título ou parceiro), adicione — a
   mesma mídia pode entrar mais de uma vez e aparece "Já adicionado (N×)" —,
   arraste para mudar a ordem, desmarque dias e **Salvar grade**.
6. **Rede → Telas → Novo**: escolha o estabelecimento (host, que define o
   fuso), a playlist do passo 5, o nome e, se quiser, a identificação do
   aparelho. Use o botão de QR Code da linha (**Gerar código**): o código
   aparece em destaque, com o tempo restante.
7. Faça o papel da TV com os passos do `Indoor.http` usando esse código: a
   tela passa a **Online** (a lista se atualiza a cada minuto).
8. Entre de novo como o dono: em **Onde meus anúncios passam** a TV aparece
   somente leitura, com o estabelecimento e a conexão.
9. Como operadora, em **Rede → Parceiros**, a coluna **Mídias ativas** mostra
   o uso do plano; edite o parceiro e aumente **Mídias ativas (plano)** para
   o dono conseguir ativar a imagem.
10. **Desvincular TV** na linha da tela: a próxima sincronização da TV recebe
    401 e a tela volta a *Aguardando pareamento*. Trocar o estabelecimento ou
    a playlist da tela não desvincula a TV.
11. **Rede → Fontes de notícias:** a Agência Brasil já vem cadastrada. Use
    **Coletar agora** (ícone de setas) e clique na linha para ver as notícias
    com as fotos. Sem internet na máquina da API, a fonte aparece "Com erro"
    com a mensagem.
12. **Rede → Playlists → editar:** "Uma notícia a cada" 2, marque
    **Embaralhar**; deixe **Relógio** e **Clima** marcados. Na grade, clique
    no 📌 do primeiro item para ele não entrar no sorteio e salve. Na TV, em
    até 1 minuto a fila passa a intercalar notícias, com a hora e a
    temperatura no canto (o clima precisa da cidade preenchida no cadastro do
    estabelecimento).

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

TV de verdade na rede: veja `player/README.md` (API ouvindo em `*` — não em `0.0.0.0`, que deixa o painel lento —,
endereço `https://<IP do PC>:7216`).


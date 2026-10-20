# Guia de implantação

Como colocar a Mídia Indoor em produção: API, banco, storage, painel e TVs.
Para rodar na sua máquina, veja [COMO_TESTAR.md](COMO_TESTAR.md).

## Visão geral

```text
  Navegador (painel)  ──https──►  API (.NET 9)  ──►  PostgreSQL
        │                            │
        │ PUT direto (URL assinada)  │ token do R2 (só o bucket)
        ▼                            ▼
              Cloudflare R2 (bucket privado "midias", teto de 10 GB)
                             ▲
  TVs (app Android) ──https──┘ download (URL assinada, 24 h)
         └──────────https──────────►  API  (pareamento e sincronização)

  Celular (QR da TV, 4G) ──https──►  API  /d/{codigo}  (diretório público)
```

Topologia recomendada: **dois subdomínios do mesmo domínio**, ambos com
certificado válido:

| Serviço | Exemplo |
|---|---|
| API | `https://api.suaempresa.com.br` |
| Painel | `https://painel.suaempresa.com.br` |

O **diretório das TVs** (ADR-018) é uma página servida pela própria API em
`/d/{codigo}`: quem lê o QR code da TV abre no celular, pelo 4G. Por isso a
**API precisa estar na internet com HTTPS** (o painel pode ficar restrito).
O mesmo endereço serve o **QR rastreado** dos anúncios (`/q/{codigo}`, ADR-023),
que conta as leituras e redireciona. O endereço vai no parâmetro `INDOOR.URL_PUBLICA` (Atlas Studio → Parâmetros; `Indoor__UrlPublica` só como valor inicial). Prefira um endereço curto
(ex.: `https://tv.suaempresa.com.br`, apontando para a mesma API): quanto
mais curto, menos denso o QR code e mais longe ele é lido.

O login do painel usa cookie `Secure` com `SameSite=None`. No mesmo domínio
(subdomínios), o navegador não trata o cookie como de terceiros.

## 1. Pré-requisitos

- **Servidor da API:**
  - Windows com IIS e o *ASP.NET Core Hosting Bundle 9*, ou Linux com o
    runtime ASP.NET Core 9 atrás de nginx/Caddy;
  - HTTPS com certificado válido. As TVs em release recusam certificado
    inválido.
- **PostgreSQL 14+**, com um banco e um usuário próprios.
- **Storage: Cloudflare R2** (recomendado, ADR-025):
  - Crie um bucket privado `midias` em *R2 Object Storage*. Anote o **Account
    ID** (lado direito do painel do R2).
  - Em *Manage R2 API Tokens*, crie um token com **Object Read & Write** e
    escopo **só no bucket `midias`**. Anote o *Access Key ID* e o *Secret
    Access Key* (aparece uma vez).
  - Gratuito: **10 GB**, sem taxa de saída. A API nunca passa do teto
    configurado (padrão 9 GB, máximo 10 GB). Como garantia extra, ligue um
    alerta de cobrança na conta da Cloudflare (*Billing → Notifications*).
- **Ou Supabase** (o storage antigo): URL do projeto e chave `service_role`.
  Gratuito: 1 GB, e o projeto **pausa depois de 7 dias sem uso**.
- **Servidor do painel:** IIS com o módulo **URL Rewrite**. O `web.config`
  usa rewrite para as rotas do Angular.
- **Máquina de build do painel:** o `atlas-framework` precisa estar clonado e
  compilado ao lado do repositório (ver `web/README.md`). O servidor só
  recebe arquivos estáticos.

## 2. API

### 2.1 Publicar

```powershell
cd api
dotnet publish src/Atlas.Web -c Release -o ..\publicacao\api
```

### 2.2 Configuração (variáveis de ambiente)

Segredos nunca vão em `appsettings` (ADR-008). No IIS, use as variáveis de
ambiente do pool ou o `web.config` gerado (`<environmentVariables>`). No
Linux, use o `Environment=` do serviço systemd.

| Variável | Obrigatória | Exemplo / observação |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | sim | `Production` |
| `ConnectionStrings__AtlasDb` | sim | `Host=…;Port=5432;Database=indoor;Username=indoor;Password=…` |
| `Jwt__Key` | sim | Frase aleatória com 64 caracteres ou mais. **Diferente** da de desenvolvimento. Trocar depois exige parear todas as TVs de novo |
| `PlatformSettings__InitialAdminPassword` | 1ª subida | Senha inicial do `admin` (ver 2.4) |
| `Indoor__UrlPainel` | sim | `https://painel.suaempresa.com.br`. Vai nos e-mails e é a origem liberada no CORS |
| `Indoor__Armazenamento__R2__Conta` | sim (R2) | Account ID da Cloudflare |
| `Indoor__Armazenamento__R2__ChaveAcesso` | sim (R2) | Access Key ID do token do bucket |
| `Indoor__Armazenamento__R2__SegredoAcesso` | sim (R2) | Secret Access Key. **Segredo crítico**: nunca no painel nem nas TVs |
| `Indoor__Armazenamento__R2__Bucket` | não | Padrão `midias` |
| `Indoor__Armazenamento__Supabase__Url` | sem R2 | `https://<projeto>.supabase.co` |
| `Indoor__Armazenamento__Supabase__ChaveServico` | sem R2 | Chave `service_role`. **Segredo crítico** |
| `Indoor__Armazenamento__CopiarDoSupabase` | migração | `true` com R2 **e** Supabase configurados: na subida, copia para o R2 o que só está no Supabase (ver 2.7) |
| `Indoor__ProxiesConfiaveis__0` | atrás de proxy | IP do proxy reverso (ex.: `127.0.0.1` com nginx na mesma máquina). Sem ele, os limites por IP (pareamento e diretório) veem só o IP do proxy. No IIS *in-process* não é necessário |

**Parâmetros operacionais (ADR-026):** e-mails de alerta, horário e tempo
dos alertas, teto do storage, intervalos das TVs e das coletas **não são
variáveis de ambiente**. Ficam em **Atlas Studio → Parâmetros**, grupo
"Mídia Indoor", e valem em até 1 min, sem reiniciar:

| Parâmetro | Padrão | O que é |
|---|---|---|
| `INDOOR.URL_PUBLICA` | vazio | Endereço HTTPS da API na internet (ex.: `https://tv.suaempresa.com.br`), base do QR do diretório (`/d`) e do QR rastreado (`/q`). Vazio ou `localhost`: sem QR do diretório, QR dos anúncios direto ao destino e leituras não contadas |
| `INDOOR.ANUNCIE.WHATSAPP` | vazio | Seu WhatsApp para quem quer anunciar. Preenchido: "Quer anunciar aqui?" no QR da TV e botão na página do diretório |
| `INDOOR.ALERTAS.EMAILS` | `[]` | E-mails da operadora (lista JSON). Vazio: só registra no log |
| `INDOOR.ALERTAS.TV_OFFLINE_MINUTOS` | `15` | Minutos sem sincronizar até o alerta (mínimo 5) |
| `INDOOR.ALERTAS.HORA_INICIO` / `HORA_FIM` | `7` / `22` | Só alerta nesse horário, no fuso do local da TV |
| `INDOOR.ARMAZENAMENTO.LIMITE_TOTAL_MB` | `9216` | Teto do storage da rede; acima de `10240` vale 10 GB |
| `INDOOR.DISPOSITIVO.INTERVALO_SINCRONIZACAO_SEGUNDOS` | `60` | Mínimo 30 |
| `INDOOR.DISPOSITIVO.VALIDADE_TOKEN_DIAS` | `30` | Token da TV |
| `INDOOR.DISPOSITIVO.VALIDADE_URL_DOWNLOAD_HORAS` | `24` | Links de download das TVs |
| `INDOOR.COLETA.ATIVA` | `true` | Coleta de notícias, clima e cotações |
| `INDOOR.COLETA.NOTICIAS_MINUTOS` / `CLIMA_MINUTOS` / `COTACOES_MINUTOS` | `60` / `30` / `30` | Mínimo 15 |

Na primeira subida, a API cadastra os que faltam com o valor que estiver na
configuração antiga (`Indoor:Alertas:Emails`, etc.) ou com o padrão. Depois
disso, vale o que está no painel.

Com o R2 configurado, ele é o storage (mesmo com o Supabase também
configurado). Sem nenhum dos dois, a API sobe, mas as rotas de mídia
respondem 503.

**Saída para a internet:** a API lê os feeds de notícias (ex.:
`agenciabrasil.ebc.com.br`), abre as páginas das notícias para achar a foto
de destaque, baixa as imagens e consulta o Open-Meteo
(`api.open-meteo.com` e `geocoding-api.open-meteo.com`) e as cotações do
Banco Central (`olinda.bcb.gov.br`). Libere HTTPS de
saída para esses endereços no firewall/proxy do servidor. Sem isso, a API
funciona, as fontes aparecem "Com erro" no painel e as TVs ficam sem
notícias novas e sem clima.

### 2.3 Banco

A API aplica as migrations sozinha ao subir, junto do catálogo de entidades
e das triggers de histórico. Para aplicar por script, revisado pelo DBA,
veja `api/docs/deployment/Implantacao-de-Migrations.md`.

### 2.4 Primeira subida: administrador e ícones

Na versão adotada da plataforma (`v0.4.0-alpha.1`), o usuário `admin`, o
grupo Administradores e o catálogo de ícones só são criados pelo *seed* do
ambiente **Development**. Em Production a API aplica as migrations, mas não
semeia nada. Na primeira implantação:

1. De uma máquina de confiança com acesso ao banco de produção, rode a API
   **uma vez** como Development, apontando para o banco de produção, com a
   senha inicial do `admin`:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ConnectionStrings__AtlasDb = "<connection string de produção>"
   $env:Jwt__Key = "<a mesma chave de produção>"
   $env:PlatformSettings__InitialAdminPassword = "<senha forte>"
   dotnet run --project src/Atlas.Web --no-launch-profile
   ```
2. Quando o log mostrar que a aplicação subiu, encerre com Ctrl+C. O seed é
   idempotente: rodar de novo não duplica nada.
3. Remova a `PlatformSettings__InitialAdminPassword` e troque a senha do
   `admin` no primeiro login.

### 2.5 Studio: módulos, menus, grupos e permissões

Com a API já tendo subido uma vez:

```powershell
psql -h <servidor> -U <usuario> -d indoor -v ON_ERROR_STOP=1 -f api/docs/scripts/studio-indoor.sql
```

O script é idempotente e termina listando as rotas e as permissões de cada
grupo.

### 2.6 Conferir

- `https://api.suaempresa.com.br/scalar` abre a documentação da API.
- No log da subida: `CORS do bucket R2 liberado para https://painel...`. Se
  aparecer o aviso de CORS (o token não administra o bucket), configure no
  painel da Cloudflare, em *R2 → midias → Settings → CORS Policy*:

  ```json
  [{ "AllowedOrigins": ["https://painel.suaempresa.com.br"],
     "AllowedMethods": ["PUT", "GET", "HEAD"], "AllowedHeaders": ["*"], "MaxAgeSeconds": 3600 }]
  ```

- No painel, **Rede → Clientes** mostra a barra **Armazenamento da rede**. Uma
  hora depois da subida, ela mostra "Bucket medido" (o inventário).
- Com Supabase: em *Storage* aparece o bucket privado `midias`, criado na subida.

### 2.7 Migrar do Supabase para o R2

1. Configure as variáveis do R2 **sem** tirar as do Supabase e ponha
   `Indoor__Armazenamento__CopiarDoSupabase=true`.
2. Reinicie a API. No log: `Cópia do Supabase para o R2: N copiado(s)...`. Se
   parar no meio, reinicie: ela continua do que falta. Se o total passar do
   teto, nada é copiado (o log diz quanto falta liberar).
3. Confira no painel uma mídia tocando numa TV (a TV baixa de novo pelo R2 na
   próxima sincronização sem `versaoGrade`, ou quando a URL de 24 h vencer).
4. Tire `CopiarDoSupabase` e as variáveis do Supabase e reinicie.

## 3. Painel (web)

### 3.1 Endereço da API

Edite `web/public/assets/config.json`. Ele também pode ser editado depois,
direto no servidor, sem recompilar:

```json
{ "apiUrl": "https://api.suaempresa.com.br" }
```

### 3.2 Publicar no IIS

```powershell
cd web
npm install
npm run publicar -- -Destino "\\servidor\c$\inetpub\painel"
# ou, para levar à mão: .\scripts\publicar-iis.ps1 -Empacotar -BaseHref "/"
```

O script compila, faz backup da versão no ar e espelha os arquivos. O
cabeçalho de `web/scripts/publicar-iis.ps1` explica destino, base href e
como reverter.

### 3.3 E-mail (SMTP)

O responsável de cada cliente (anunciante) recebe a senha temporária por e-mail. Entre
como `admin`, vá em **Atlas Studio → SMTP**, configure o servidor de e-mail e
envie o teste. Sem SMTP, a mensagem fica pendente em
`platform.email_message` e pode ser reprocessada depois.

### 3.4 Usuários da operadora

Em **Atlas Studio → Usuários**, crie quem vai cadastrar parceiros, clientes
e TVs e coloque essas pessoas no grupo **Operadora**. O responsável de cada
cliente entra sozinho no grupo **Cliente** quando o cliente é cadastrado.
Parceiros (os locais das TVs) não têm acesso ao sistema (ADR-022).

## 4. App das TVs

### 4.1 Gerar o APK

```powershell
cd player
.\scripts\gerar-apk.ps1 -Modo Release -ApiUrl https://api.suaempresa.com.br
# player/dist/midia-indoor-1.0.1+2-release-armeabi-v7a.apk (o script já subiu a versão)
```

O script sobe a versão do `pubspec.yaml`, gera um APK por arquitetura (o
universal passa de 50 MB) e mostra no fim o que preencher em Versões do app:
envie o `armeabi-v7a`, que roda nas TV box de 32 e de 64 bits. Para testar na rede local,
sem certificado válido, use `-Modo Profile` (o padrão). Opções:
`Get-Help .\scripts\gerar-apk.ps1 -Detailed`.

A versão vem do `version` do `pubspec.yaml` (ex.: `1.2.0+7`): `1.2.0` é o
nome que aparece no painel e `7` é o **código**, usado pela atualização
automática (4.3). **Aumente o número depois do `+` em toda versão nova.**
O APK universal tem ~25 MB; se passar de 50 MB, gere um por arquitetura com
`--split-per-abi` e envie o `app-armeabi-v7a-release.apk` (o das TV box
comuns) ou o `arm64-v8a`, conforme o aparelho.

O APK de release só aceita certificado válido. O endereço também pode ser
trocado na própria TV, na tela de pareamento.

**Assinatura:** o projeto assina o release com a chave de debug. Antes de
distribuir, crie uma chave própria (`keytool -genkey …`) e configure o
`signingConfig` em `player/android/app/build.gradle.kts`, seguindo a
documentação do Flutter sobre assinatura de apps Android. Atualizações de um
APK só instalam por cima se forem assinadas com a mesma chave.

### 4.2 Instalar na TV

1. Na TV, ative as **Opções do desenvolvedor**: toque 7 vezes no número da
   versão em *Sobre*.
2. Instale o APK de um destes jeitos:
   - pela rede: `adb connect <IP da TV>:5555` e depois
     `adb install -r app-release.apk`;
   - por pen drive, com um gerenciador de arquivos, liberando *fontes
     desconhecidas*.
3. Libere **Sobrepor a outros apps** para o Mídia Indoor (Android 10 ou mais
   novo). Sem isso, o app não abre sozinho quando a TV liga.
4. Abra o app, digite o código gerado em **Rede → Telas** e pareie.
5. Desligue e ligue a TV para confirmar que o app abre sozinho.
6. **Atualização sem confirmação (recomendado):** torne o app *dono do
   aparelho* (device owner). Só funciona com a TV **sem nenhuma conta Google
   cadastrada** (remova em *Configurações → Contas* antes, se houver):

   ```bash
   adb shell dpm set-device-owner br.com.midiaindoor.midia_indoor_player/.AdministradorAparelho
   ```

   Deve responder `Success: Device owner set to package …`. No painel de
   informações da TV (botão OK do controle), a linha **Atualização** passa a
   dizer "instalação automática". Atenção: um app device owner não pode ser
   desinstalado pelo menu; para desfazer, restaure a TV de fábrica.
   Sem device owner a atualização funciona, mas o
   Android mostra "Deseja instalar esta atualização?" na TV e alguém precisa
   apertar **Instalar** no controle (e liberar *Instalar apps desconhecidos*
   para o Mídia Indoor na primeira vez).

### 4.3 Atualizar as TVs (ADR-021)

1. Gere o APK com `player/scripts/gerar-apk.ps1` (4.1): ele aumenta o código
   do `pubspec.yaml` (`1.2.0+7` → `1.2.1+8`). O APK tem de ser **assinado com
   a mesma chave** das versões anteriores (gere sempre na mesma máquina até
   configurar uma chave própria). Faça o commit do `pubspec.yaml`.
2. Em **Rede → Telas → Versões do app**, escolha o APK, confira código e nome
   e envie. A versão entra **suspensa**.
3. Marque uma ou mais telas como **TV de teste** (Editar tela) e mude a
   distribuição para **Só TVs de teste**. Em até 1–2 minutos a TV baixa,
   instala e reabre. Confira no painel de informações dela e na coluna
   **App** da lista de telas.
4. Funcionou? Mude para **Todas as TVs**. Cada TV instala na sincronização
   seguinte. Deu problema? **Suspenda**: quem ainda não instalou não instala.
   O Android não volta para uma versão menor; corrija e envie uma versão nova.

TVs com o app de antes desta função (sem código na sincronização) aparecem no
aviso da página: precisam de uma instalação manual (4.2), uma vez.

## 5. Checklist de produção

- [ ] HTTPS válido na API e no painel.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` e todos os segredos em variáveis de
      ambiente (nada em `appsettings`).
- [ ] `Jwt:Key` e senha do `admin` diferentes das de desenvolvimento.
- [ ] `Indoor:UrlPainel` com o endereço real do painel.
- [ ] `INDOOR.URL_PUBLICA` (Parâmetros) com o endereço HTTPS da API na internet. Teste no
      celular **pelo 4G** (Wi-Fi desligado): ligue o diretório numa tela, abra
      **Diretório de anunciantes** em Rede → Telas e leia o QR code.
- [ ] Admin e ícones criados (2.4) e `studio-indoor.sql` aplicado (2.5).
- [ ] SMTP configurado e testado.
- [ ] Bucket `midias` privado no R2, token só com "Object Read & Write" nele,
      segredo só na API. Alerta de cobrança ligado na Cloudflare.
- [ ] Barra **Armazenamento da rede** em Rede → Clientes abaixo de 70%.
- [ ] Backup do PostgreSQL agendado. As mídias ficam no R2, fora do backup do
      banco.
- [ ] APK assinado com chave própria e guardada em lugar seguro.
- [ ] `INDOOR.ALERTAS.EMAILS` (Atlas Studio → Parâmetros) com o e-mail da operadora; desligar uma TV de
      teste e receber o alerta (15 min) e o "de volta ao ar".
- [ ] TVs como device owner (4.2, passo 6) e uma versão de teste instalada
      sozinha pela página **Versões do app** (4.3).
- [ ] Uma TV de teste pareada, tocando, aparecendo **Online** no painel, e
      continuando a tocar com a rede desligada.

## Pontos de atenção

- **CORS:** a lista fixa do `Program.cs` da plataforma só tem endereços de
  desenvolvimento. A origem do painel em produção é liberada pelo produto a
  partir de `Indoor:UrlPainel` (`Atlas.Web/Business/CorsDoPainel.cs`).
- **Diretório público:** `/d/{codigo}` e `/api/v1/publico/diretorio/{codigo}`
  são anônimos por decisão (ADR-018), com limite de 60 acessos por minuto por
  IP e cache de 60 s. Mostram só o que cada cliente aceitou publicar.
- **Documentação da API exposta:** a plataforma publica `/scalar` e
  `/openapi` em qualquer ambiente. Se não quiser expor, bloqueie esses
  caminhos no proxy ou no IIS.
- **Teto do storage (ADR-025):** a rede inteira cabe em
  `INDOOR.ARMAZENAMENTO.LIMITE_TOTAL_MB` em Parâmetros (padrão 9 GB, nunca mais que os 10 GB
  gratuitos do R2). A cota de cada cliente é definida no cadastro (padrão
  200 MB); a soma das cotas pode passar do teto, mas o teto é o que vale:
  envio que não cabe é recusado. A operadora recebe e-mail a 70%, 85% e 95%.
  As imagens das notícias também ficam no bucket (até 30 por fonte, em
  `noticias/`) e param de ser baixadas a 90% do teto. Arquivos sem dono há
  mais de 6 h são apagados de hora em hora.
- **Licença das notícias:** a Agência Brasil (CC BY) exige o crédito, que a
  TV mostra. Outras fontes só com licença para exibição pública.
- **Clima:** usa a cidade do cadastro do estabelecimento. Para cidades com
  nome repetido, use "Cidade - UF" (ex.: "São José - SC").

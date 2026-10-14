# Guia de implantação

Como colocar a Mídia Indoor em produção: API, banco, storage, painel e TVs.
Para rodar na sua máquina, veja [COMO_TESTAR.md](COMO_TESTAR.md).

## Visão geral

```text
  Navegador (painel)  ──https──►  API (.NET 9)  ──►  PostgreSQL
        │                            │
        │ PUT direto (URL assinada)  │ chave service_role
        ▼                            ▼
              Supabase Storage (bucket privado "midias")
                             ▲
  TVs (app Android) ──https──┘ download (URL assinada, 24 h)
         └──────────https──────────►  API  (pareamento e sincronização)
```

Topologia recomendada: **dois subdomínios do mesmo domínio**, ambos com
certificado válido:

| Serviço | Exemplo |
|---|---|
| API | `https://api.suaempresa.com.br` |
| Painel | `https://painel.suaempresa.com.br` |

O login do painel usa cookie `Secure` com `SameSite=None`. No mesmo domínio
(subdomínios), o navegador não trata o cookie como de terceiros.

## 1. Pré-requisitos

- **Servidor da API:**
  - Windows com IIS e o *ASP.NET Core Hosting Bundle 9*, ou Linux com o
    runtime ASP.NET Core 9 atrás de nginx/Caddy;
  - HTTPS com certificado válido. As TVs em release recusam certificado
    inválido.
- **PostgreSQL 14+**, com um banco e um usuário próprios.
- **Projeto no Supabase:**
  - Anote a URL do projeto e a chave `service_role` (em Project Settings →
    API).
  - No plano gratuito são 1 GB de storage e 50 MB por arquivo.
  - O plano gratuito **pausa o projeto depois de 7 dias sem uso**. Para
    produção, avalie o plano pago.
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
| `Indoor__Armazenamento__Supabase__Url` | sim | `https://<projeto>.supabase.co` |
| `Indoor__Armazenamento__Supabase__ChaveServico` | sim | Chave `service_role`. **Segredo crítico**: nunca no painel nem nas TVs |
| `Indoor__ProxiesConfiaveis__0` | atrás de proxy | IP do proxy reverso (ex.: `127.0.0.1` com nginx na mesma máquina). Sem ele, o limite de tentativas de pareamento vê só o IP do proxy. No IIS *in-process* não é necessário |
| `Indoor__Dispositivo__IntervaloSincronizacaoSegundos` | não | Padrão `300` |
| `Indoor__Dispositivo__ValidadeTokenDias` | não | Padrão `30` |
| `Indoor__Dispositivo__ValidadeUrlDownloadHoras` | não | Padrão `24` |

Sem a configuração do Supabase a API sobe, mas as rotas de mídia respondem
503.

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
- No Supabase, em *Storage*, aparece o bucket privado `midias`. Ele é criado
  na subida.

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

O responsável de cada parceiro recebe a senha temporária por e-mail. Entre
como `admin`, vá em **Atlas Studio → SMTP**, configure o servidor de e-mail e
envie o teste. Sem SMTP, a mensagem fica pendente em
`platform.email_message` e pode ser reprocessada depois.

### 3.4 Usuários da operadora

Em **Atlas Studio → Usuários**, crie quem vai cadastrar os parceiros e as
TVs e coloque essas pessoas no grupo **Operadora**. O dono de cada parceiro
entra sozinho no grupo **Proprietário de Parceiro** quando o parceiro é
cadastrado.

## 4. App das TVs

### 4.1 Gerar o APK

```powershell
cd player
flutter build apk --release --dart-define=API_URL=https://api.suaempresa.com.br --dart-define=VERSAO_APP=1.0.0
# build/app/outputs/flutter-apk/app-release.apk
```

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

## 5. Checklist de produção

- [ ] HTTPS válido na API e no painel.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` e todos os segredos em variáveis de
      ambiente (nada em `appsettings`).
- [ ] `Jwt:Key` e senha do `admin` diferentes das de desenvolvimento.
- [ ] `Indoor:UrlPainel` com o endereço real do painel.
- [ ] Admin e ícones criados (2.4) e `studio-indoor.sql` aplicado (2.5).
- [ ] SMTP configurado e testado.
- [ ] Bucket `midias` privado no Supabase. Chave `service_role` só na API.
- [ ] Backup do PostgreSQL agendado. As mídias ficam no Supabase, fora do
      backup do banco.
- [ ] APK assinado com chave própria e guardada em lugar seguro.
- [ ] Uma TV de teste pareada, tocando, aparecendo **Online** no painel, e
      continuando a tocar com a rede desligada.

## Pontos de atenção

- **CORS:** a lista fixa do `Program.cs` da plataforma só tem endereços de
  desenvolvimento. A origem do painel em produção é liberada pelo produto a
  partir de `Indoor:UrlPainel` (`Atlas.Web/Business/CorsDoPainel.cs`).
- **Documentação da API exposta:** a plataforma publica `/scalar` e
  `/openapi` em qualquer ambiente. Se não quiser expor, bloqueie esses
  caminhos no proxy ou no IIS.
- **Plano gratuito do Supabase:** o limite é 1 GB no total. A cota de cada
  parceiro é definida no cadastro (padrão 200 MB). Some as cotas antes de
  prometer espaço.

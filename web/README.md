# Mídia Indoor — painel

Painel Angular do **Sistema de Mídia Indoor**: a operadora cadastra os
parceiros e as TVs, as fontes de notícias e monta as playlists com os
anúncios da rede; cada parceiro (anunciante) envia e mantém os próprios
vídeos e imagens. Consome a biblioteca `@atlas/framework` e conversa com a API em
[`../api`](../api/README.md).

## Pré-requisitos

- Node.js e npm compatíveis com Angular 21.
- O repositório `atlas-framework` clonado **ao lado** da pasta deste
  repositório e com o `dist` gerado (`npm install` e `npm run build` no
  framework). O `package.json` aponta para
  `file:../../atlas-framework/dist/atlas/framework`:

  ```text
  <pasta>/
    atlas-framework/
    -Sistema-de-M-dia-Indoo/
      api/  web/  player/
  ```

- A API rodando (padrão `https://localhost:7216`, configurável em
  `public/assets/config.json`), com o banco migrado e o script
  `api/docs/scripts/studio-indoor.sql` aplicado: os módulos, menus e grupos do
  painel vêm do Studio.

## Rodar local

```bash
npm install
npm start
```

`npm start` roda `scripts/gerar-versao.js` antes do `ng serve`: a versão do
`package.json` e as notas do `CHANGELOG.md` são carimbadas na aplicação
(`src/app/versao.ts`, gerado e fora do Git).

O painel precisa rodar em `http://localhost:4200`: é a origem de front
liberada no CORS da API.

## Telas

| Rota | Quem usa | O quê |
|---|---|---|
| `/rede/parceiro` | Operadora | Parceiros: cadastro (cria o login do responsável), espaço em MB, plano de mídias ativas, bloquear/desbloquear |
| `/rede/tela` | Operadora | TVs da rede (o hardware é dela): estabelecimento (host), playlist, identificação do aparelho, código de pareamento, conexão, desvincular |
| `/rede/playlist` | Operadora | Playlists da rede (uma playlist toca em várias telas), com a composição da fila (informativo a cada N, cartões de tempo e cotações, embaralhar, relógio, clima); "Clonar" duplica com a grade |
| `/rede/playlist/:id/grade` | Operadora | Mídias ativas de todos os parceiros (busca, filtro, "Já adicionado (N×)"), ordem do loop, 📌 posição fixa e dias da semana de cada item |
| `/rede/fonte-noticia` | Operadora | Feeds de notícias (RSS/Atom): cadastro, "Coletar agora", situação da última leitura e as notícias guardadas |
| `/indoor/midia` | Parceiro | Biblioteca: título, duração, período, ativar/inativar dentro do plano ("Mídias ativas: x de y"), substituir arquivo, pré-visualizar, espaço usado |
| `/indoor/midia/enviar` | Parceiro | Envio de vídeo MP4 ou imagem JPG/PNG direto ao storage, com SHA-256 e progresso; com `?substituir={id}`, troca o arquivo de uma mídia |
| `/indoor/tela` | Parceiro | **Onde meus anúncios passam**: TVs com mídia ativa dele, somente leitura |

O parceiro só vê as próprias mídias: o isolamento é feito pela API.

## Estrutura

```text
src/app/
  app.config.ts        provideAtlasFramework + interceptors
  app.routes.ts        rotas; módulos de negócio por routePrefix/route_key do Studio
  app-shell.component  casca autenticada (layout + menus vindos da API)
  features/<feature>/  models, services, configs, pages (rede, telas, midias, playlists, noticias)
  shared/              utilitários (paginação, formulário, dias da semana, bytes, duração)
public/assets/config.json   endereço da API, lido em tempo de execução
```

## Envio de mídia

O arquivo não passa pela API:

1. `POST /midias/envio` — a API confere tipo, tamanho (até 50 MB) e o espaço
   do parceiro e devolve uma URL assinada do storage;
2. o navegador faz o `PUT` do arquivo direto nessa URL (com progresso);
3. `POST /midias` com o SHA-256 calculado no navegador — a API baixa o
   arquivo, confere tipo e hash e só então cadastra a mídia.

O SHA-256 usa `crypto.subtle`, que só existe em página segura (HTTPS ou
`localhost`). Fora disso o painel envia sem o hash e a API calcula sozinha.

## QR code das mídias

Na edição da mídia, a aba **QR code** mostra só os campos do tipo escolhido
(`criarCamposQrCode` em `features/midias/configs/midia.config.ts`, com
`hidden`). O serviço manda o campo do tipo como `qrDestino` e zera os outros
(`corpoQrCode`); a API valida e monta o conteúdo (link, `wa.me`, `tel:` ou
Pix copia e cola). A prévia da lista usa o `angularx-qrcode` com o mesmo
conteúdo que vai para a TV.

## Publicar

`npm run publicar` gera o build e publica no IIS — ver o cabeçalho de
`scripts/publicar-iis.ps1` para destino, base href e backups.

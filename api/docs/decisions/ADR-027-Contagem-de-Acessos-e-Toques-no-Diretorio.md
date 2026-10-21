# ADR-027: Contagem de Acessos e Toques no Diretório

## Status

Aceito

## Data da decisão

2026-10-21

## Contexto

O diretório da TV (ADR-018) é a página que o celular abre ao ler o QR code
(`/d/{codigo}`): lista os anunciantes da grade com WhatsApp, Instagram e site,
e o botão "Quer anunciar aqui?". O QR rastreado dos anúncios (ADR-023) já conta
leituras, mas o diretório não contava nada: nem quantas pessoas abriram, nem
quem tocou em qual contato. É o número que o anunciante quer ver ("quantos me
chamaram pela TV?") e o que a operadora quer saber sobre cada local.

## Decisão

- **Acesso:** abrir `/d/{codigo}` conta um acesso da tela.
- **Toque:** a página marca cada contato (`data-toque`, dono e tipo) e o botão
  "anuncie". No clique, envia `navigator.sendBeacon` para
  `POST /d/{codigo}/toque?e=contato&c={cliente}&t={tipo}` (ou `e=anuncie`). O link
  abre direto, **sem passar pela API**: se a contagem falhar, a pessoa chega ao
  WhatsApp do mesmo jeito. A CSP da página libera `connect-src 'self'` só para isso.
- **Só conta o que a página oferece:** contato de quem não está no diretório
  daquela TV, tipo que o anunciante não publicou ou "anuncie" sem o WhatsApp da
  operadora são ignorados (a resposta é 204 do mesmo jeito).
- **Robôs não contam:** pré-visualização de link (WhatsApp, Facebook, "bot",
  "preview") e requisição sem navegador.
- **Privacidade:** como no QR rastreado, sem IP. `Visitante` é um hash diário
  de IP + navegador + data (com segredo do processo) para contar pessoas
  diferentes por dia.
- **Tabela** `indoor.interacao_diretorio` (tela, parceiro, evento, cliente e
  contato no toque, momento, visitante), **sem chave estrangeira**: o histórico
  sobrevive à exclusão da TV ou do cliente. Os ids internos vêm do DTO do
  diretório e ficam fora do JSON público (`[JsonIgnore]`).
- **Onde aparece:**
  - **Relatório de veiculação** (cliente e operadora): cartão "Toques no
    diretório" por contato, coluna por dia e linha no resumo para o WhatsApp.
  - **Rede → Telas → Diretório de anunciantes:** últimos 30 dias de acessos,
    pessoas, toques em contatos, toques em "Quer anunciar aqui?" e toques por
    anunciante (`GET /api/v1/business/rede/telas/{id}/diretorio/uso`).

## Alternativas consideradas

- **Redirecionar pela API** (`/d/{codigo}/ir/...`, como o `/q`): conta com mais
  certeza, mas `tel:` e o Pix (copiar) não funcionam por redirecionamento, e
  a API fora do ar deixaria a pessoa sem o contato.
- **Contar no navegador com ferramenta de terceiros:** exporia os visitantes a
  outra empresa e exigiria afrouxar a CSP.

## Consequências

- Recarregar a página conta outro acesso; "pessoas por dia" é o número que
  não repete.
- Um navegador que bloqueia `sendBeacon` não conta o toque (o contato abre do
  mesmo jeito): o número é um piso, não o total exato.
- Uma linha por acesso e por toque: com milhares de leituras por mês a tabela
  cresce; quando pesar, agregar por dia.

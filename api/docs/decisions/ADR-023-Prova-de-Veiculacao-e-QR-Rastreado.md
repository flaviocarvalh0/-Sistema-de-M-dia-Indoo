# ADR-023: Prova de Veiculação e QR Rastreado

## Status

Aceito

## Data da decisão

2026-10-20

## Contexto

As duas reuniões do conselho consultivo (`founder/board.md` e `founder/board-v2.md`)
apontaram o mesmo risco nº 1: o anunciante paga e não vê nada. Sem registro do que
passou nem de quem reagiu, a renovação vira um ato de fé e o cliente cancela no
segundo ou terceiro mês.

O que o sistema sabia era só "a TV sincronizou". Isso não diz o que passou na tela,
nem quantas vezes, nem se alguém leu o QR code.

## Decisão

### Registro de exibições (no player)

- O loop chama o `RegistradorExibicoes` quando um **anúncio** sai da tela, com mídia,
  início, duração e se foi até o fim. Notícias e cartões não contam. Vídeo que nem
  abriu não é registrado; o que falhou no meio fica como **interrompido**.
- Cada exibição leva um **UUID gerado na TV** e a marca **sem conexão** quando a
  última sincronização não chegou à API.
- Fica no SQLite (`exibicao`, banco v4) até ser enviada. No máximo 20 mil pendentes:
  acima disso, as mais antigas saem.
- Depois de uma sincronização que respondeu, envia em lotes de até 500, **no máximo a
  cada 15 min** (ou já, com 300 pendentes). Erro: mantém e tenta no próximo ciclo.
- Parear com outra tela apaga as pendentes (não podem ir com o token da tela nova).

### Recebimento (na API)

- `POST /api/v1/dispositivo/exibicoes`, com o token da TV, até 500 por lote e limite
  de 20 lotes por minuto por tela.
- **Idempotente** pelo par (tela, id local): reenviar não duplica.
- Descarta mídia desconhecida, início com mais de 35 dias ou mais de 10 min no futuro
  e duração fora de 0 a 2 h. Responde 200 com aceitas, já recebidas e descartadas; a
  TV apaga o lote inteiro.
- `indoor.registro_exibicao` grava os ids de tela, parceiro, mídia e cliente **sem
  chave estrangeira**: o histórico de veiculação sobrevive à exclusão da TV ou da mídia.

### QR rastreado

- Mídias com QR de **Link, WhatsApp ou Instagram** ganham um código de 8 caracteres
  (`Midia.CodigoQr`). A TV passa a codificar `{Indoor:UrlPublica}/q/{codigo}`, um
  endereço mais curto, que dá um QR menos denso.
- `GET /q/{codigo}` é anônimo: grava a leitura e responde **302** para o destino,
  sem cache e com `Referrer-Policy: no-referrer`. Limite de 30 por IP por minuto.
  Código inexistente responde 404.
- **Telefone e Pix ficam diretos**: o Pix copia e cola precisa ser o próprio conteúdo
  do QR (o app do banco lê o texto), e redirecionar para `tel:` não é confiável em
  todos os celulares. O relatório mostra "não rastreado".
- Sem `Indoor:UrlPublica`, a TV mostra o destino direto, como antes.
- **Privacidade:** a leitura não guarda IP. `Visitante` é um HMAC diário (segredo do
  processo) de IP + navegador + data, cortado em 16 hex: conta leitores únicos por
  dia sem identificar ninguém.

### Relatório

- `GET /api/v1/business/cliente/relatorio` (o próprio cliente) e
  `GET /api/v1/business/rede/clientes/{id}/relatorio` (operadora). Período de até 93
  dias; sem datas, do dia 1 do mês até hoje, no fuso do cliente.
- Traz exibições (concluídas, interrompidas, sem conexão), tempo na tela, leituras e
  leitores únicos do QR, e as quebras por TV, por anúncio, por dia e **por hora local
  da TV**.
- `MedeTelaLigada = false`: a página avisa que o registro diz que o app exibiu, **não
  que a TV estava ligada** e na entrada certa.
- No painel, **"Copiar resumo para o WhatsApp"** monta o texto curto para o
  anunciante. O PDF ficou de fora, como o conselho sugeriu.

## Justificativa

- Gravar na TV e enviar em lote é o mesmo desenho offline-first do player (ADR-013):
  a queda de internet não apaga a prova.
- O UUID da TV dispensa transação distribuída: a TV só apaga depois do 200, e o
  reenvio é inofensivo.
- O redirecionamento curto conta a reação sem depender de integração com WhatsApp ou
  Instagram.

Alternativas rejeitadas:

- **Inferir exibições pela grade e pelo status Online:** conta o que deveria ter
  passado, não o que passou.
- **Enviar cada exibição na hora:** uma requisição a cada anúncio e perda de dados
  sem internet.
- **Guardar o IP de quem leu o QR:** desnecessário para contar e um risco pela LGPD.

## Consequências

### Ficou mais fácil

- Mostrar ao anunciante onde e quantas vezes passou, e quantas pessoas reagiram.
- Responder "a TV estava sem internet?" (exibições marcadas como sem conexão).

### Ficou mais difícil

- O relatório ainda **não detecta a TV desligada ou na entrada errada**. Isso
  depende de HDMI-CEC ou captura de tela, que ficam para depois.
- A contagem de leitores únicos repete alguém se a API reiniciar no mesmo dia (o
  segredo do HMAC muda).
- O relógio da TV define o horário da exibição: TV com hora errada distorce o
  relatório. Uma verificação do relógio fica para depois.
- `Indoor:UrlPublica` passa a ser necessária também para contar as leituras do QR.

## Referências

- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first
- [ADR-017](ADR-017-QR-Code-nos-Anuncios.md) — QR code nos anúncios
- [ADR-018](ADR-018-Diretorio-de-Anunciantes-da-TV.md) — Diretório (endereço público)

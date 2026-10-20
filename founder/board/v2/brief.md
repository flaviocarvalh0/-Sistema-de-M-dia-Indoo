# Briefing do conselho — Mídia Indoor, 2ª reunião

Leia como um estranho: tudo o que você precisa está aqui e nos arquivos citados. Responda em **português**.

## Contexto

Na 1ª reunião o conselho votou 3 × "FUND IF" (nota média 4,7/10). A ata está em
`/home/user/-Sistema-de-M-dia-Indoo/founder/board.md` e os pareceres anteriores em
`/home/user/-Sistema-de-M-dia-Indoo/founder/board/offers.md`, `monopoly.md` e `product.md`.
Leia a ata antes de opinar. O briefing original do negócio está em
`/home/user/-Sistema-de-M-dia-Indoo/founder/board/brief.md`.

O fundador voltou com uma estratégia reformulada. **Atenção ao status de cada item:** ele
descreveu tudo no presente, mas a verificação no código mostrou o que existe e o que é plano.

## Fatos novos sobre o mercado

- A cidade tem cerca de **20 mil habitantes** e **7 academias**.
- Piloto continua com até 10 TVs, operado pelo fundador. Preço e modelo de cobrança
  **continuam indefinidos**. Parceiro ganha anúncio grátis e talvez algum repasse.

## A estratégia reformulada (com status verificado)

| # | Item | Status real |
|---|---|---|
| 1a | **Log de exibição em lote**: o player grava cada exibição concluída no armazenamento local e envia em lotes à API a cada 15 min (sobrevive a quedas de rede) | **Plano** (não implementado) |
| 1b | **QR code rastreado**: todo QR passa por um encurtador da API (`/q/{id}`) que conta a leitura e redireciona para o WhatsApp/Instagram do anunciante | **Plano** |
| 1c | **Relatório mensal automático em PDF** antes da renovação: total de exibições, horários de pico, leituras do QR | **Plano** |
| 2a | **Heartbeat de reprodução**: o ping envia a mídia tocando agora e se o app está em primeiro plano; parada > 10 min marca o ponto "Fora de Exibição" | **Plano** (hoje só existe a sincronização a cada 60 s e o alerta de TV sem sincronizar por 15 min) |
| 2b | **Verificação do relógio** do aparelho contra a API | **Plano** (o filtro do dia já roda na TV, no fuso do local, mesmo offline) |
| 3a | **Grade por ponto e por faixa horária (dayparting)** | **Parcial**: cada TV já pode ter a própria playlist (anúncio por ponto) e cada item já tem dias da semana; **faixa horária não existe** |
| 3b | **Dominar o nicho**: fechar contrato com as 7 academias da cidade (monopólio local em fitness/saúde) | **Plano comercial** (nenhum contrato fechado informado) |
| 3c | **Loop longo (20–30 min)**, adequado à permanência de 45–60 min no treino, comportando até 60 cotas de anúncio sem cansaço visual | **Parcial**: o carrossel com "tamanho da volta" (30 s a 1 h) já existe e garante a mesma frequência a todos; a capacidade de 60 cotas é uma conta do fundador, não testada com público |
| 4a | **Migrar o armazenamento para Cloudflare R2** (10 GB grátis, sem taxa de egresso) | **Plano** (hoje Supabase gratuito, 1 GB; o storage está atrás de uma interface própria, o que facilita trocar) |
| 4b | **Monitoramento externo** do servidor (UptimeRobot) com alerta por webhook | **Plano** |
| 4c | **Produção do primeiro vídeo inclusa** na adesão (template Canva) | **Plano comercial** |

## Lacunas que a estratégia não menciona (constatação, não opinião)

- Backup automático do banco fora do celular-servidor e teste de restauração (condição da 1ª ata).
- Contrato escrito com os parceiros (prazo, exclusividade, repasse) (condição da 1ª ata).
- Preço, modelo de cobrança, custo por TV, custo de visita técnica: nenhum número informado.
- HDMI-CEC / captura de tela (sugeridos na 1ª ata para detectar TV desligada ou na entrada errada).
- Taxa de renovação a medir no 3º mês.

## O que o fundador pediu

A nova nota do conselho, o parecer atualizado de cada lente (Monopólio, Oferta, Produto e
**CFO**) e os **requisitos finais para lançar o piloto**.

# Ata do conselho — Mídia Indoor, 2ª reunião

**Votação: 4 × "investe se" (FUND IF), nenhum "passa". Nota média: 5,0 / 10.**
As três lentes da 1ª reunião subiram de 4,7 para **5,7**. O CFO participou pela primeira vez e deu 3.

| Lente | 1ª reunião | 2ª reunião | Voto |
|---|---|---|---|
| Monopólio | 4 | **5** | FUND IF |
| Oferta | 5 | **6** | FUND IF |
| Produto | 5 | **6** | FUND IF |
| CFO | — | **3** | FUND IF |

Pareceres completos em `founder/board/v2/`. Briefing com o status verificado de cada item em
`founder/board/v2/brief.md`.

**Por que as notas subiram pouco:** o plano responde às objeções certas e foi bem
priorizado, mas **quase tudo continua plano**. Na verificação do código, os itens novos —
log de exibição, QR rastreado, relatório, heartbeat, faixa horária, R2, UptimeRobot — ainda
não existem. Já existem anúncio por ponto (playlist por TV), dias da semana por item e o
loop com "tamanho da volta". As quatro lentes concordam que a nota sobe de verdade quando o
**primeiro relatório verdadeiro** chegar a um anunciante real.

## O que a nova estratégia resolve (consenso)

- **Ataca o risco nº 1 da 1ª ata.** Log em lote, QR rastreado e relatório antes da
  renovação são exatamente a parte da experiência que faltava.
- **O mercado pequeno ganhou nome.** As 7 academias de uma cidade de ~20 mil habitantes são
  um nicho que dá para dominar, com público, horário e permanência conhecidos.
- **A barreira do anunciante cai.** O primeiro vídeo incluso tira o passo "faça um vídeo".
- **A infraestrutura deixa de ter teto e ponto cego.** O R2 resolve a cota de 1 GB, e o
  monitoramento externo resolve o servidor que cai junto com o próprio alarme.

## O que continua aberto ou piorou (mais de uma lente)

1. **A prova ainda pode ser falsa** (Oferta, Produto). O heartbeat diz se o app está
   tocando, não se a **TV está ligada e na entrada certa**. CEC e captura de tela saíram do
   plano. Um relatório com "1.200 exibições" e a TV desligada na visita **destrói a
   confiança mais rápido do que não ter relatório**.
2. **Nenhum contrato com as academias** (Monopólio, CFO, Produto). O "monopólio" é uma
   intenção. Sem prazo, exclusividade e repasse por escrito, o ponto é de quem pagar mais.
3. **Sem preço, sem custo, sem repasse** (as quatro). Não há como saber se dá lucro.
4. **Backup com restauração testada continua fora do plano** (as quatro). Era condição da 1ª
   ata. Perder o banco agora apagaria também o histórico de exibições e de cobrança.
5. **60 cotas é uma conta, não demanda medida** (Monopólio, Oferta, Produto, CFO). Numa
   cidade de 20 mil habitantes, vender 60 anunciantes é uma meta comercial pesada, e muitas
   cotas diluem a lembrança de cada uma.
6. **QR com pouca leitura vira prova contra** (Oferta, Produto). Quase ninguém escaneia no
   meio do treino. Um relatório com "3 leituras" pode adiantar o cancelamento. É preciso
   escolher antes quais números o relatório promete.
7. **Concentração num nicho só** (Monopólio, Oferta, CFO). Se 2 ou 3 academias saem, o
   pacote "fitness da cidade" perde valor para todos os clientes ao mesmo tempo. E, para o
   anunciante, 7 telas com o mesmo público podem valer pouco mais que 1.

### Riscos novos que a estratégia cria

- **Poder de barganha invertido** (Monopólio): anunciar que quer "as 7" faz cada academia
  saber que é peça necessária e pedir repasse maior.
- **Exclusividade que conflita com o anunciante** (Monopólio): uma academia pode vetar
  anúncios de concorrentes ou de suplementos. Precisa estar no contrato.
- **Vídeo incluso sem limite** (Oferta, CFO): cada cliente novo, inclusive quem substitui um
  cancelamento, consome horas do fundador. Limitar a 1 vídeo e 1 revisão e cobrar as trocas.
- **Franquias que acabam de repente** (CFO): 60 vídeos de 50 MB são 3 GB; com 2–3 versões por
  cliente, 6–9 GB, perto dos 10 GB do R2. Alerta de espaço e limpeza antes de 80%.

### Onde as lentes discordam

- **Produto quer cortar; os outros aceitam o pacote.** A lente de Produto acha que a lista
  cresceu em vez de encolher e quer **adiar** a faixa horária, o PDF e a migração para o R2
  (que só entraria com alerta de 70% da cota) até o 3º mês. A lente de CFO lembra que 60
  vídeos já não cabem em 1 GB. Leitura do conselho: o R2 só é urgente se a venda passar de
  ~15–20 vídeos; a faixa horária e o PDF podem esperar.
- **Formato do relatório.** Oferta e CFO aceitam o PDF. Produto diz que o comerciante local
  vive no WhatsApp: **uma mensagem com três números e um link** é lida; um PDF anexado, não.
- **O que conta como exibição.** Produto exige que o relatório some **só os itens que o log
  registrou como concluídos**, nunca inferindo pelo status "Online". Num loop de 20–30 min,
  10 min parado é um terço da volta: alguns anunciantes perdem a vez e outros não, e o
  relatório tem de mostrar isso por anunciante.
- **Contas do CFO sobre o loop:** 20–30 min com 60 cotas dá **20–30 s por anúncio**, ou
  **2–3 exibições por hora**. Quem treina 45–60 min vê cada anúncio de 1,5 a 3 vezes. O teto
  é de **420 cotas-TV** (7 × 60), ou **60 anunciantes** se vendido como pacote de rede.
  Notícias, clima e cotações tomam parte do loop, então o tempo real por anúncio é menor.

## Requisitos finais para lançar o piloto pago

Consolidado dos quatro pareceres. **Nenhum anunciante paga antes de todos os itens
marcados como obrigatórios.**

### Software (obrigatório)

- [ ] **Log de exibição** no player: cada item concluído é gravado no banco local (mídia,
  tela, início, duração) e enviado em lote; "tocou offline" separado de "não tocou".
  Conferido por **7 dias seguidos em pelo menos 1 TV** e numa **visita sem aviso a 3 pontos**.
- [ ] **QR rastreado** (`/q/{id}`): conta a leitura e redireciona (WhatsApp, Instagram,
  link); testado com um anunciante real.
- [ ] **Heartbeat de reprodução** com alerta "sincroniza mas não exibe", testado
  **desligando uma TV de propósito**.
- [ ] **Detecção de tela realmente ligada** (HDMI-CEC ou captura de tela periódica). Nos
  aparelhos sem suporte, o relatório diz **"não verificado"** em vez de contar.
- [ ] **Relatório mensal automático** antes do vencimento, por anunciante: exibições por TV
  e por dia, horas fora de exibição e leituras do QR. **Entregue por WhatsApp com link**
  (PDF opcional). Antes do 1º cliente pagante, um anunciante real precisa ler e dizer o que
  entendeu.
- [ ] **Alerta de relógio** da TV errado.
- [ ] **Backup diário do banco fora do celular**, com **uma restauração feita, cronometrada
  e documentada**.
- [ ] **Monitoramento externo do servidor** ativo (confirmar se os termos do plano gratuito
  permitem uso comercial).
- [ ] **Alerta de armazenamento em 70–80%** da franquia e política de limpeza de mídias
  inativas.
- [ ] **Cobrança mínima** (pode ser planilha, mas melhor no sistema): vencimento,
  pago/atrasado, aviso antes de bloquear, inadimplência e churn por mês.

### Comercial e contratos (obrigatório)

- [ ] **Contratos assinados com a maioria das 7 academias** (ex.: 5 de 7), com prazo
  mínimo, exclusividade de mídia indoor no local, **repasse por escrito** (de preferência %
  da receita do ponto), **quem liga a TV todo dia** e o que acontece quando ela fica
  desligada, e as regras de veto de anunciantes concorrentes.
- [ ] **Oferta escrita** (`/founder-offer`): nome do pacote, vídeo incluso com escopo
  limitado (1 vídeo, 1 revisão, preço da troca), garantia ligada ao tempo no ar **medido
  pelo software** e número máximo de cotas por loop.
- [ ] **Limite de cotas do piloto definido por observação:** medir em 2 academias quanto
  tempo as pessoas olham a tela e quantos anúncios lembram. Até lá, **bem abaixo de 60**.
- [ ] **Quais números o relatório promete**, decididos antes de vender (exibições, QR ou
  outro), com uma meta realista.

### Financeiro (obrigatório)

- [ ] Planilha de entradas com **valores reais** e a fonte de cada um: TV/box, instalação,
  box reserva, repasse, internet e energia (quem paga), horas e custo de visita, hospedagem,
  domínio, horas por vídeo, preço por cota, ocupação esperada, inadimplência, churn.
- [ ] **Preço por cota e modelo** (por TV ou pacote "7 academias") escritos na proposta.
- [ ] **Break-even em cotas por TV** calculado e **abaixo de 50% da capacidade vendável**.
- [ ] **Teto de caixa** do piloto e prazo de payback aceitável.
- [ ] Revisão de um contador antes da primeira fatura.

### Pode esperar até o 3º mês (Produto)

- [ ] Faixa horária (dayparting).
- [ ] Geração de PDF.
- [ ] Migração para o R2, a menos que a venda passe de ~15–20 vídeos antes.

### Gatilhos para depois do piloto

- [ ] **Taxa de renovação medida ao fim do 3º mês** com preço definido.
- [ ] **Uma página com o caminho de expansão** pós-academias (próxima categoria de saúde na
  mesma cidade ou academias das cidades vizinhas) e o gatilho para começar.
- [ ] Ficha por aparelho, box reserva e re-pareamento rápido antes de passar de 7–10 TVs.

## A versão mais forte deste negócio, agora

O conselho vê mais força do que na 1ª reunião. O fundador escolheu um nicho que dá para
dominar e mirou a falha certa. A versão mais forte é: **"o público fitness da cidade, com
prova"** — as TVs de quase todas as academias, sob contrato, vendidas como um pacote único
ao comércio local, com o primeiro vídeo feito pela operadora e uma **mensagem mensal no
WhatsApp** mostrando onde o anúncio passou, por quanto tempo a tela esteve realmente ligada
e quantas pessoas reagiram.

O que separa isso de um "FUND" puro são três entregas: **o log e o relatório funcionando com
dados verdadeiros**, **os contratos com as academias** e **os números do CFO**. Enquanto o
plano não vira código e assinatura, a recomendação continua: não cobrar, não fixar preço e
não passar de 10 TVs.

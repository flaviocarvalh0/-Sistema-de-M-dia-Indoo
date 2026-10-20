# Lente de Oferta — 2ª reunião
Score: 6 (antes: 5) | Vote: FUND IF

## O que a nova estratégia resolve
- **Probabilidade percebida, no papel.** Log de exibição em lote, QR rastreado e relatório mensal em PDF antes da renovação respondem exatamente ao meu risco nº 1. O desenho é bom: o lote sobrevive à queda de rede e o QR mede reação, não só exibição. Mas tudo isso é **plano**. Hoje o anunciante continua sem nenhuma prova. Esse é o principal motivo de a nota subir só 1 ponto.
- **Esforço do anunciante.** Incluir o primeiro vídeo (template Canva) tira o passo "faça um vídeo" do caminho do cliente. Foi uma condição minha e entrou na estratégia, ainda como plano comercial.
- **Diferença contra o post impulsionado.** Um loop longo em academia, com permanência de 45–60 min, e uma grade por ponto (que já existe) dão ao anunciante um público cativo e local. Isso é um argumento que não depende de preço.
- **Teto do armazenamento.** A migração para o R2 fica fácil porque o storage já está atrás de uma interface própria. Isso tira a cota de 1 GB do caminho de crescimento.

**Por que 6 e não mais:** dou crédito a um plano coerente e bem priorizado. Mas o valor percebido pelo comprador só muda quando ele recebe o primeiro relatório verdadeiro. Se a prova de veiculação, o QR rastreado e o relatório forem entregues e funcionarem nas 10 TVs, a nota vai a 7. Se o 3º mês mostrar renovação com preço definido, ela pode passar disso.

## O que ainda não resolve ou piorou
- **Ainda não há preço, modelo de cobrança nem garantia.** Sem preço não existe oferta. A garantia "TV apagada = dia não cobrado" não aparece na estratégia.
- **A prova pode ser falsa.** O heartbeat diz se o app está tocando, mas não se a TV está ligada e na entrada certa. Sem HDMI-CEC ou captura de tela, o PDF pode contar exibições com a tela apagada. Um relatório errado destrói a confiança mais rápido do que a falta de relatório.
- **O mercado ficou menor e mais claro.** São 20 mil habitantes e 7 academias. Isso é bom para alcançar o comprador, mas limita o total de anunciantes e o crescimento. Quantos negócios locais pagariam, e quanto, segue sem resposta.
- Backup testado e cobrança com vencimento (condições da 1ª ata) continuam fora da estratégia.

## Riscos novos que a estratégia cria
- **60 cotas por loop.** A conta é do fundador e não foi testada. Vender 60 cotas numa cidade de 20 mil habitantes é uma meta comercial pesada. Com muitas cotas, cada anúncio vale menos para quem o compra. Com poucas, a conta não fecha. Isso depende de `/founder-consumer` e `/founder-cfo`.
- **O relatório pode jogar contra.** Se as leituras do QR forem baixas, o PDF mostra ao cliente que o anúncio não deu resultado e adianta o cancelamento. O relatório precisa vir junto com uma meta realista e uma sugestão de troca do criativo.
- **Vídeo incluso sem limite.** Com um operador só, cada vídeo, revisão e troca consome tempo. É preciso definir o escopo: um vídeo, número de revisões e prazo.
- **Concentração em academias.** O público é praticamente o mesmo nas 7 academias. Para o anunciante, "7 telas" pode valer pouco mais que 1. Além disso, um contrato perdido derruba parte da oferta.

## Requisitos para lançar o piloto pago
1. Log de exibição gravado no player e enviado à API, com "tocou offline" separado de "não tocou", conferido em pelo menos 1 TV por 7 dias seguidos.
2. QR rastreado (`/q/{id}`) contando leituras e redirecionando para WhatsApp e Instagram, testado com um anunciante real.
3. Relatório mensal gerado automaticamente e enviado antes do vencimento, com exibições por TV e faixa horária e leituras do QR.
4. Heartbeat de reprodução com alerta "sincroniza mas não exibe", e uma forma de detectar TV desligada ou na entrada errada (CEC ou captura). Se não houver, o relatório deve dizer claramente que não mede isso.
5. Preço, modelo de cobrança e economia por TV definidos com dados de `/founder-consumer`, `/founder-pricing` e `/founder-cfo`.
6. Oferta escrita (`/founder-offer`): vídeo incluso com escopo limitado, garantia ligada ao tempo no ar medido pelo software, nome do pacote e número máximo de cotas por loop.
7. Backup diário fora do celular, com uma restauração testada e registrada, e monitoramento externo ativo.
8. Vencimento e aviso antes do bloqueio, para cada cliente pagante.

## Perguntas ao fundador
- Quem são os 5 primeiros anunciantes que você visitaria amanhã, e de qual ramo?
- Quantas leituras de QR por mês um anunciante precisa ver para renovar?
- Quantas cotas você vende de verdade nos primeiros 3 meses, e não quantas cabem no loop?
- Que garantia você aguenta pagar se 2 das 10 TVs ficarem apagadas por uma semana?
- Quantas das 7 academias já disseram sim, e com que contrapartida?

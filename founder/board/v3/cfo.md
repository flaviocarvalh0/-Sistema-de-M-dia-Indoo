# Lente de CFO — 3ª reunião
Score: 4 (antes: 3) | Vote: FUND IF

**Resumo:** o risco de **custo de infraestrutura** foi bem tratado. O risco de saber se o
negócio dá lucro **não mudou**: continua sem preço, repasse, custo por TV ou planilha. Não
inventei valores; tudo abaixo é fórmula ou aritmética sobre o que foi entregue.

## O que a entrega resolve financeiramente
- **Teto de armazenamento com travas** (limite de 9 GB, reserva antes da URL, inventário de
  hora em hora, alertas a 70/85/95%) elimina o risco que apontei de a franquia acabar de
  repente e a cobrança vir no cartão. A conta do pior caso (60 vídeos de 50 MB = 3 GB; 6–9 GB
  com versões) agora tem um freio em código. **Falta ligar:** a conta na Cloudflare ainda não
  existe e o contrato do R2 não foi testado. Até lá, o teto é teoria.
- **Heartbeat + log** tornam mensurável a garantia "dia sem tela no ar não é cobrado". Antes era
  um passivo ilimitado; agora há uma base de cálculo, com a ressalva de que ela não vê TV
  desligada.
- **Relatório e diretório** sustentam a **renovação**, que é a variável que decide a receita.
  Ainda não há dado de renovação: não existe cliente pagante.
- **Parâmetros no painel** reduzem horas de operação (e-mails, limites sem redeploy).

## O que não mudou
- **Zero número de custo ou receita.** `founder/numbers.json` não existe. Nada sobre box,
  instalação, internet, energia, hora do fundador, preço por cota, repasse, inadimplência ou
  churn. As fórmulas da 2ª reunião (break-even por TV, payback por ponto) continuam sem entradas.
- **Nenhum controle de cobrança no sistema.** Não há vencimento, pago/atrasado, aviso antes do
  bloqueio, inadimplência ou churn.
- **Backup do banco ausente.** Agora o banco guarda o histórico de exibições, que é a prova de
  que o fundador pode cobrar. Perder o celular apaga a prova e a cobrança. Pior que na 2ª ata.
- **Repasse e exclusividade sem contrato** (e sem campo no cadastro).
- **O fundador consumiu horas em software, não em venda.** O custo de oportunidade é o item mais
  caro e não está em nenhuma planilha.

## Contas desta rodada (sem inventar entradas)
- **Contribuição do relatório sobre o churn:** se o churn mensal for c, a vida média do cliente
  é 1/c meses. A prova de veiculação só se paga se reduzir c o bastante para que
  `P × (1/c_novo − 1/c_antigo)` supere o custo de produzi-la, que agora é quase só horas de
  desenvolvimento já gastas. O que falta é medir c.
- **Garantia por dia sem tela:** custo máximo por cliente = `P × dias sem tela ÷ 30`. Com
  2 de 10 TVs apagadas por 7 dias, e pacote de rede em 10 TVs, o desconto é de `P × 7/30` por
  cliente, e como o heartbeat só vê o app, **a TV desligada fica fora e o passivo é do
  fundador**.
- **Contagem inflada** (dados de teste) distorce qualquer cálculo de custo por reação.
- **Armazenamento:** 9 GB de teto ÷ 50 MB = 180 arquivos no máximo; com 3 versões por cliente
  são ~60 clientes. O teto deixa de ser o limite de receita.

## Riscos financeiros novos
- **Ponto único de falha na prova** (celular sem backup guardando o histórico).
- **Garantia sem medição de tela ligada.** Se a promessa for "dia sem tela no ar", o heartbeat
  não mede o que o cliente vê.
- **Gasto recorrente oculto:** servidor na nuvem para o backup e o R2 (se passar do gratuito)
  entram no custo fixo e ainda não têm valor.

## Requisitos (restam, financeiros)
1. Planilha de entradas com valores reais e a fonte de cada um (itens da 2ª ata, sem mudança).
2. Preço por cota e modelo na proposta; break-even em cotas por TV < 50% da capacidade vendável.
3. Teto de caixa e prazo de payback do piloto.
4. **Backup externo com restauração cronometrada antes da primeira fatura** (agora é a condição
   mais barata e mais urgente).
5. Controle mínimo de cobrança (vencimento, pago, atrasado, churn mensal), mesmo em planilha.
6. Conta e bucket do R2 criados e teste de contrato rodado; termos do UptimeRobot confirmados.
7. Contador antes da primeira fatura.

**Próximo passo:** criar `founder/numbers.json` (unidade = cota-mês) e rodar o cálculo de margem,
break-even e cenários (preço −10%, ocupação −20%, custos +15%).

*Não é aconselhamento contábil: um contador deve revisar a tributação antes de emitir a primeira cobrança.*

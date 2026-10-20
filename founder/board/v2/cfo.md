# Lente de CFO — 2ª reunião
Score: 3 (1ª reunião: não participou) | Vote: FUND IF

**Resumo:** hoje não dá para dizer se o negócio dá lucro. Não há preço, custo por TV, custo de visita nem valor de repasse. Todos os números abaixo saem do próprio briefing ou são fórmulas. Nenhum valor foi inventado.

## O que a nova estratégia resolve financeiramente
- **Log + QR + relatório (1a–1c):** é o que sustenta a **renovação**, e a renovação é a variável que decide a receita. Sem isso o churn não pode ser medido nem reduzido. Custo: horas de desenvolvimento, sem desembolso. O PDF automático custa CPU do servidor, não horas do fundador, e é melhor que um relatório manual, que custaria tempo a cada cliente por mês.
- **Heartbeat (2a):** é a base para uma garantia do tipo "TV apagada = dia não cobrado". Sem ele, a garantia é um passivo que não se consegue medir.
- **Nicho de academias (3b) e loop longo (3c):** definem o **estoque vendável**, o primeiro número concreto do modelo (ver abaixo).
- **R2 (4a):** tira o limite de 1 GB, que barrava a receita em cerca de 20 vídeos. **UptimeRobot (4b):** protege a receita contra a queda silenciosa do servidor. As duas soluções custam zero, com as ressalvas de franquia indicadas abaixo.

## O que falta para saber se dá lucro (os números necessários)
**Investimento por ponto:** TV/box e suporte, instalação (material + horas), box reserva.
**Custo mensal por ponto:** repasse ao parceiro (fixo ou % da receita do ponto), internet e energia (quem paga: o parceiro ou o fundador), horas de visita técnica por mês × valor da hora + deslocamento, taxa de reposição de hardware (vida útil da box).
**Fixos da operação:** hospedagem/servidor (hoje o celular; e se for preciso migrar?), domínio, armazenamento acima da franquia, e-mail, conta da loja/APK, ferramenta de cobrança, contador.
**Venda:** preço por cota (por TV ou pacote "7 academias"), duração do contrato, cotas vendidas por TV (ocupação), inadimplência (%), churn mensal (%), horas de venda por cliente novo.
**Produção do vídeo incluso:** horas do fundador por vídeo × valor da hora, mais as trocas de criativo por cliente/ano.
**Caixa:** quanto o fundador pode investir antes de o piloto se pagar.

## Contas que já dá para fazer com os dados do fundador
*(aritmética sobre os números do briefing; não é previsão)*
- **Tempo por anúncio:** loop de 20–30 min = 1.200–1.800 s ÷ 60 cotas = **20–30 s por cota**, considerando 100% do loop com anúncios. Notícias, clima e cotações entram no mesmo loop, então o tempo real por cota é menor, ou o loop fica maior.
- **Frequência:** **2–3 exibições por hora** por TV (60 ÷ 30 a 60 ÷ 20). Um aluno que treina 45–60 min vê cada anúncio **1,5 a 3 vezes**.
- **Exibições/dia por cota** = horas abertas do local × 60 ÷ duração do loop (horas abertas: dado a coletar).
- **Estoque máximo:** 7 academias × 60 = **420 cotas-TV**. Se cada parceiro usa uma cota grátis, sobram **413 vendáveis**. Vendido como pacote de rede (1 cota em cada uma das 7 TVs), o teto é **60 anunciantes**. Se 60 negócios de uma cidade de 20 mil habitantes vão comprar é uma hipótese a testar, não um dado.
- **Armazenamento (pior caso, 50 MB/arquivo):** 60 vídeos = 3 GB. Isso não cabe no Supabase (1 GB) e cabe no R2 (10 GB). Com 2–3 versões guardadas por cliente: 6–9 GB, **perto do limite de 10 GB**. A box também precisa de cerca de 3 GB livres.
- **Break-even por TV (fórmula):**
  `cotas_BE = (repasse + internet/energia + visitas + depreciação da box + rateio dos fixos) ÷ [P × (1 − inadimplência) − custo variável por cota]`
  O custo variável por cota inclui horas de vídeo ÷ meses de permanência do cliente.
- **Payback por ponto** = (box + instalação) ÷ contribuição mensal do ponto.
- **Exemplo hipotético, não é dado do fundador:** se o custo mensal de um ponto fosse X e cada cota deixasse X/10 após os custos variáveis, o break-even seria 10 cotas, ou 1/6 da capacidade. A estrutura mostra que o risco está na **ocupação**, não na capacidade.

## Riscos financeiros novos
- **Vídeo incluso escala com o churn:** cada cliente novo, inclusive os que substituem quem cancelou, consome horas do fundador. Com o estoque cheio são dezenas de vídeos. É preciso limitar (1 vídeo, 1 revisão) e cobrar as trocas.
- **Franquias que acabam de repente:** o R2 cobra acima de 10 GB e por operações acima da franquia; o UptimeRobot gratuito tem intervalo mínimo de checagem e termos de uso (confirmar se permitem uso comercial). Isso exige alerta de espaço e política de limpeza antes de chegar a 80%.
- **Repasse indefinido + exclusividade:** fechar as 7 academias pode exigir repasse alto. Um repasse fixo com ocupação baixa dá prejuízo por ponto. O melhor é um % da receita do ponto.
- **Concentração:** com um nicho só, se 2–3 academias saírem, o pacote "rede" perde valor para todos os clientes ao mesmo tempo.
- **Garantia de tempo no ar:** sem heartbeat, é um custo ilimitado.
- **Lacunas da 1ª ata ainda abertas:** backup e contrato. A perda do banco também apaga o histórico de cobrança e de exibições.

## Requisitos financeiros para lançar o piloto pago
1. Planilha de entradas preenchida com **valores reais** (orçamentos, contas), cada um com a fonte, cobrindo todos os itens da seção 2.
2. **Preço por cota e modelo** (por TV / pacote) definidos e escritos na proposta comercial.
3. **Break-even em cotas por TV calculado** e abaixo de 50% da capacidade vendável.
4. **Repasse definido em contrato** (de preferência um % da receita do ponto), com prazo e exclusividade.
5. **Limite do vídeo incluso** por escrito (nº de vídeos e revisões; preço da troca).
6. **Teto de caixa** do piloto definido (investimento máximo em boxes e instalação) e prazo de payback aceitável.
7. **Controle de cobrança**, mesmo que em planilha: vencimento, pago ou atrasado, inadimplência e churn por mês.
8. Alerta de armazenamento em 80% da franquia e confirmação dos termos do R2 e do UptimeRobot.
9. Backup externo com restauração testada **antes** da primeira fatura.

**Próximo passo:** rodar `/founder-cfo` com essas entradas (`founder/numbers.json`, unidade = cota-mês) para obter margem, break-even, caixa necessário e cenários (preço −10%, ocupação −20%, custos +15%).

*Não é aconselhamento contábil: um contador deve revisar a tributação antes de emitir a primeira cobrança.*

# Lente de Monopólio
Score: 4/10 | Vote: FUND IF

## O que é forte
- **Mercado pequeno e concreto para dominar**: uma cidade, poucos pontos, anunciante local. A lente de Monopólio prefere isso: começar com uma fatia grande de um mercado pequeno.
- **O ativo defensável não é a TV, é o ponto.** Quem fecha primeiro as melhores academias, clínicas e lanchonetes da cidade ocupa um espaço físico que um copiador não consegue duplicar. Esse é o único "monopólio" possível aqui.
- **Embrião de efeito de rede local**: mais pontos tornam o pacote melhor para o anunciante; mais anunciantes pagam o repasse que segura o parceiro. O diretório por QR code também cresce em valor com cada anunciante.
- **Software acima da média para o tamanho**: player offline-first com SHA-256, troca atômica de grade, atualização escalonada. Não é ruptura tecnológica (não é 10x), mas reduz o custo de operar cada TV, o que vira economia de escala se a rede crescer.

## O que mataria o negócio (top 3, o mais perigoso primeiro)
1. **Sem fosso contra o copiador do mês seis.** TV + box Android + software de sinalização é coisa que outras pessoas já vendem. Se o parceiro não tem contrato nem exclusividade, outro operador oferece um repasse maior e leva o ponto. Quem são esses concorrentes e quanto cobram é pergunta aberta para **/founder-competitors**.
2. **Churn de anunciante por falta de prova.** Sem relatório de exibição e sem contagem de QR, o anunciante não consegue ver retorno; renova por simpatia, não por resultado. Sem retenção não há rede, e sem rede não há efeito de rede.
3. **Operação que depende de uma pessoa e de um celular.** O servidor caseiro sem backup e o fundador como único técnico são um teto de escala e um ponto único de falha. A rede não sobrevive à primeira semana de doença ou à perda do aparelho.

## Riscos operacionais e o que prever no software
- **Parceiro saindo / ponto perdido** → no sistema: contrato do parceiro (vigência, repasse, cláusula de exclusividade de categoria) e alerta de vencimento; histórico de desempenho por ponto para justificar o repasse. Processo: contrato assinado antes de instalar.
- **Churn de anunciante** → prova de veiculação: log de exibições gravado no player (offline, enviado na sincronização) e relatório mensal por cliente (vezes, TVs, horários). Contagem de leituras do QR por anúncio via link de redirecionamento. Alerta interno de "cliente em risco" (poucas leituras, vencimento próximo).
- **Queda de internet no ponto** → o player já resiste. Falta: o relatório deve distinguir "tocou offline" de "não tocou", e o painel deve mostrar há quanto tempo a grade daquela TV está desatualizada. Política de não vender anúncio com prazo curto em TV cronicamente offline.
- **Tela desligada, entrada errada, volume** → heartbeat com sinais do aparelho (app em primeiro plano, HDMI-CEC quando disponível, captura de tela periódica de baixa resolução) e alerta de "online mas não exibindo". Sem isso, a prova de veiculação é falsa.
- **Manutenção de hardware** → cadastro de equipamento por TV (modelo, data de instalação, ocorrências) e checklist de visita. Custo de troca por TV é pergunta para **/founder-cfo**.
- **Servidor caseiro** → backup automático diário do banco fora do aparelho, restauração testada, e plano de migração para hospedagem barata antes da TV 11. O limite de 1 GB do Supabase exige política de limpeza de mídias inativas.
- **Inadimplência** → já existe bloqueio; falta cobrança com vencimento e bloqueio automático após atraso.

## Perguntas que eu faria ao fundador
- Qual é o segredo: que ponto ou relação você tem na cidade que outro operador não consegue?
- Qual é exatamente o mercado pequeno: uma categoria de ponto (ex.: academias) ou uma cidade inteira?
- O que impede um parceiro de trocar você por quem pagar mais amanhã?
- Como chegam os 10 primeiros anunciantes e quanto custa cada um? (**/founder-consumer**, **/founder-pricing**)
- Por que agora, e por que você em vez de uma operadora já estabelecida?
- Quem cobre a operação se você parar por duas semanas?

## Se FUND IF: as condições
1. Contrato escrito com os parceiros do piloto, com prazo mínimo e alguma exclusividade, antes de ampliar a rede.
2. Prova de veiculação (log de exibição + leituras de QR) entregue antes de cobrar o primeiro anunciante.
3. Backup automatizado e restauração testada do servidor.
4. Um nicho escolhido e dominado (a maioria dos pontos daquela categoria na cidade) antes de expandir para outra cidade ou categoria.
5. Preço e modelo de cobrança definidos com dados reais (**/founder-pricing**, **/founder-cfo**). Sem isso, não há como julgar se o repasse ao parceiro cabe na margem.

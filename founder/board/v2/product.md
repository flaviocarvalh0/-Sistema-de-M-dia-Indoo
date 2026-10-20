# Lente de Produto — 2ª reunião
Score: 6 (antes: 5) | Vote: FUND IF

## O que a nova estratégia resolve
- **A estratégia mira a falha central que a lente de Produto apontou.** Log de exibição em lote, QR rastreado e relatório antes da renovação são exatamente a metade da experiência que faltava: o que o anunciante vê depois do upload. O desenho do log (gravar local, enviar em lote, sobreviver à queda) segue a mesma qualidade do player atual.
- **O foco ficou mais claro.** "Todas as academias da cidade" dá um público, um horário e uma permanência conhecidos. Agora dá para desenhar a experiência a partir de uma pessoa concreta: alguém entre séries, olhando a parede por alguns segundos.
- **A produção do primeiro vídeo inclusa** tira o anunciante do passo em que ele travava.
- **Por que a nota subiu só 1 ponto:** quase tudo é **plano**. Dou crédito porque o plano é coerente e a base técnica (offline, SHA-256, carrossel com frequência igual, playlist por TV, armazenamento atrás de interface) torna os itens viáveis. Mas nada disso foi entregue. A experiência do anunciante continua acabando no upload.

## O que ainda não resolve ou piorou
- **"Fora de Exibição" não detecta o caso mais comum.** App em primeiro plano e mídia tocando continuam verdadeiros com a TV desligada ou na entrada do futebol. HDMI-CEC e captura de tela sumiram do plano. Sem eles, o relatório conta exibições que ninguém viu, e a prova volta a ser falsa.
- **O limite de 10 min mede a coisa errada.** Para alerta operacional, 10 min serve. Para cobrança, o relatório deve contar só os itens que o log registrou como concluídos, nunca inferir pelo status. Num loop de 20–30 min, 10 min parado é um terço a metade de uma volta: alguns anunciantes perdem a vez inteira e outros não. O relatório precisa mostrar isso por anunciante.
- **Loop de 20–30 min com até 60 cotas não foi pensado a partir de quem treina.** Entre séries a pessoa olha a tela por alguns segundos, não por 30 s seguidos. Em 45–60 min ela cruza cada anúncio uma ou duas vezes, com atenção parcial. Mais cotas diluem a lembrança de cada uma e empurram notícias e clima, que são o motivo de olhar a tela, para longe. Em uma cidade de 20 mil habitantes, 60 anunciantes também não é a restrição real. O número é uma conta, não uma observação.
- **PDF é o formato errado para o comerciante local.** Ele vive no WhatsApp. Uma mensagem com três números (exibições, leituras do QR, melhor horário) e um link para a página do relatório é lida. Um PDF anexado, não.
- **A estratégia não corta nada.** Ela soma: R2, dayparting, verificação de relógio, UptimeRobot e o nicho inteiro. A lista ficou maior que a da 1ª reunião.
- **Backup com restauração testada e contrato com parceiro** continuam ausentes, embora fossem condições da ata.

## Riscos novos que a estratégia cria
- **Relatório bonito sobre dados frágeis.** Se o primeiro relatório disser "1.200 exibições" e o anunciante encontrar a TV desligada na academia, a confiança some de vez. Isso é pior do que não ter relatório.
- **QR de baixa leitura vira prova contra.** Numa academia, poucas pessoas vão escanear com o celular na mão no meio do treino. Um relatório com "3 leituras" pode acelerar o cancelamento. O conjunto de números do relatório precisa ser escolhido antes de prometê-lo.
- **Sete academias com uma pessoa só** multiplicam visitas técnicas antes de existir ficha por aparelho e box reserva.

## Requisitos para lançar o piloto pago
1. Log de exibição funcionando em todas as TVs, com cada item concluído registrado e enviado em lote. Ele precisa conferir com o que se vê na tela numa visita sem aviso a pelo menos 3 pontos.
2. Detecção de tela realmente ligada (CEC ou captura de tela periódica) em todas as TVs do piloto. Nos aparelhos sem suporte, o relatório marca "não verificado" em vez de contar.
3. QR rastreado em produção (`/q/{id}`) contando leituras de cada anunciante.
4. Relatório entregue por WhatsApp com link (o PDF fica opcional), mostrando por anunciante as exibições por TV e por dia, as horas fora de exibição e as leituras do QR. Antes do primeiro cliente pagante, um anunciante real precisa ler esse relatório e dizer o que entendeu.
5. Loop do piloto com limite de cotas definido por observação: medir em 2 academias quanto tempo as pessoas olham a tela e quantos anúncios lembram. Até lá, o limite fica bem abaixo de 60.
6. Backup diário fora do celular, com uma restauração feita e cronometrada, e monitoramento externo do servidor ativo.
7. Contrato assinado com cada academia do piloto, cobrindo quem liga a TV e o que acontece quando ela fica desligada.
8. Cortes explícitos até o 3º mês: dayparting, migração para o R2 (entra só com alerta de 70% da cota) e geração de PDF.

## Perguntas ao fundador
- Quanto tempo uma pessoa entre séries olha de fato a TV? Responder com observação em 2 academias (/founder-consumer).
- Qual número do relatório faz o comerciante renovar: exibições, QR ou outro? (/founder-consumer, /founder-offer)
- Quantos anunciantes cabem numa cidade de 20 mil habitantes e a que preço? (/founder-pricing, /founder-cfo)
- Quem liga a TV todo dia em cada academia, e o que o contrato diz?
- O que você vai deixar de fazer para entregar o log e o relatório primeiro?

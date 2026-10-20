# Lente de Oferta
Score: 5/10 | Vote: FUND IF

## O que é forte
- **Comprador fácil de achar.** Pequenos negócios locais da mesma cidade: dá para visitar um por um. A lente de Oferta pontua bem esse critério do mercado.
- **A dor existe.** Todo pequeno negócio quer mais clientes da vizinhança. O resultado sonhado ("gente daqui me conhecendo e me chamando no WhatsApp") é claro.
- **O resultado chega rápido.** O anúncio entra no ar em minutos e o QR code leva direto ao contato. "Tempo até o resultado" é bom.
- **A base técnica já cuida da parte mais difícil.** O player toca offline, confere os arquivos por SHA-256, se atualiza sozinho e avisa quando uma TV some. Isso é raro num piloto.

## O que mataria o negócio (top 3, o mais perigoso primeiro)
1. **Nenhuma prova de que funciona.** Hoje o anunciante paga e não recebe nada que mostre quantas vezes passou, onde, nem quantas pessoas leram o QR. A lente de Oferta chama isso de "probabilidade percebida": é o fator que mais derruba ofertas, e aqui ele está perto de zero. Sem prova, a renovação vira um ato de fé, e o churn acontece no segundo ou terceiro mês.
2. **Esforço demais para o anunciante.** O cliente precisa produzir e enviar o próprio vídeo ou imagem. Muito pequeno negócio não tem isso pronto. Se o primeiro passo é "faça um vídeo", muita gente para ali. Esforço alto derruba o valor da oferta.
3. **Virar commodity contra o anúncio de rede social.** Sem preço definido e sem uma diferença clara, a comparação natural é com impulsionar post no Instagram, e a briga vira só de preço. Quanto o comprador topa pagar e contra o que ele compara a oferta ficam em aberto (respondem /founder-competitors, /founder-consumer e /founder-pricing).

## Riscos operacionais e o que prever no software
- **Internet cai no ponto.** O player já continua tocando. Falta **registrar cada exibição localmente** (mídia, TV, horário), guardar esse registro no aparelho e enviar quando a conexão voltar. Assim a queda não apaga a prova de veiculação.
- **Tela apagada, na entrada errada ou sem volume.** "Sincronizou" não quer dizer "alguém viu". Prever: um sinal periódico de que o app está em primeiro plano, a leitura do estado da TV por HDMI-CEC quando o aparelho suportar, uma captura de tela de vez em quando e alerta para "sincroniza mas não exibe". Medir o tempo de tela no ar por TV.
- **Manutenção de hardware.** Prever um histórico por aparelho (instalação, trocas, defeitos, visitas), o tempo médio até cada TV voltar ao ar e um checklist de visita. Com uma pessoa só, cada visita custa caro. Quanto ela custa é pergunta para o /founder-cfo.
- **Churn de anunciante.** Prever um **relatório mensal automático** (exibições, telas, horários, leituras do QR por link de redirecionamento, acessos ao diretório), aviso de renovação e uma lista de clientes com engajamento caindo. O relatório é o que segura a renovação.
- **Inadimplência.** O bloqueio já existe. Falta controlar vencimentos, avisar antes de bloquear e guardar o histórico de pagamentos.
- **Servidor caseiro e cota de 1 GB.** Prever backup automático fora do celular e alerta de espaço. A cota limita quantos clientes cabem. O tamanho do limite e o custo de sair dele são perguntas para o /founder-cfo.
- **Parceiro desiste do ponto.** Registrar o acordo com cada parceiro e conseguir remanejar os anúncios para outras TVs sem quebrar o contrato com o cliente.

## Perguntas que eu faria ao fundador
- Quem exatamente compra primeiro, de qual ramo, e por que acreditaria que funciona?
- Qual resultado você promete: exibições, leituras do QR ou contatos? Dá para medir?
- Você produziria o vídeo para o cliente? Quanto isso custa em tempo?
- Que garantia você aguenta pagar? Por exemplo: "TV apagada = dia não cobrado".
- Quantos anunciantes cabem numa TV antes de cada um ficar invisível no carrossel?
- Você conseguiria cobrar o dobro e continuar sendo a escolha óbvia? Por quê?

## Se FUND IF: as condições
1. **Prova de veiculação e contagem de QR** antes de cobrar o primeiro cliente, com relatório mensal automático.
2. **Uma oferta montada, não só uma TV com anúncio:** produção do primeiro vídeo inclusa, QR rastreado, relatório, presença no diretório e uma garantia ligada ao tempo de tela no ar, que o software precisa medir.
3. **Monitoramento de "tela realmente exibindo"**, não só de sincronização.
4. **Validar preço e disposição de pagar** com compradores reais (/founder-consumer, /founder-pricing) e a economia por TV (/founder-cfo) antes de passar de 10 telas.
5. **Backup automático do servidor** e um plano para quando o armazenamento gratuito não bastar.

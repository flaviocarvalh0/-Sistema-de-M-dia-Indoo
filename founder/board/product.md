# Lente de Produto
Score: 5 | Vote: FUND IF

## O que é forte
- O player foi pensado a partir do momento que mais importa: a TV ligada no local. Ser offline-first, conferir cada arquivo por SHA-256, trocar a grade só quando tudo baixou e abrir sozinho ao ligar mostra cuidado com a parte que ninguém vê. A queda de internet, que é o risco mais citado, já está quase resolvida.
- O carrossel com frequência igual entre anunciantes é uma regra simples que o cliente entende e que dá para defender.
- Notícias, clima e cotações dão ao público um motivo para olhar a tela. Sem isso, uma tela só de anúncios vira papel de parede.

## O que mataria o negócio (top 3, o mais perigoso primeiro)
1. **O anunciante paga e não vê nada.** Não há prova de veiculação nem contagem de QR. Para o cliente, a experiência acaba no upload. Depois disso, ele tem só a sua palavra. Pequeno negócio local que não enxerga retorno cancela no segundo mês. Pela lente de Produto, essa é a falha central: a parte da experiência que justifica o pagamento não existe.
2. **A tela apagada que o painel mostra como "Online".** Sincronizar não quer dizer exibir. O dono do local desliga a TV, troca a entrada para o futebol ou baixa o brilho, e a box continua sincronizando. O anunciante perde a veiculação, o painel diz que está tudo certo e ninguém fica sabendo.
3. **Pontos de ligação frágeis numa operação de uma pessoa só.** O servidor é um celular, sem backup e sem redundância. O armazenamento é de 1 GB: cerca de 20 vídeos de 50 MB já esgotam a cota. Cobrança e renovação acontecem fora do sistema. O parceiro não tem canal para avisar que a TV quebrou. Cada passagem entre pessoas ou sistemas é um lugar onde o serviço falha sem ninguém ver.

## Riscos operacionais e o que prever no software
- **Internet caindo no local:** o player já cobre o essencial. Faltam três coisas. (a) Validar no próprio player o período e os dias de exibição pelo relógio local, com NTP e um alerta se o relógio estiver errado, para que um anúncio vencido não continue no ar quando a TV fica offline. (b) Guardar o log de exibições no aparelho e enviá-lo quando a internet voltar. (c) Mostrar no painel há quanto tempo cada TV está com a grade desatualizada.
- **Tela desligada ou na entrada errada:** usar HDMI-CEC para ligar a TV, voltar para a entrada certa e consultar o estado da tela, quando o aparelho permitir. Mandar uma captura de tela periódica para o painel. Agendar liga/desliga conforme o horário do local. Mandar um heartbeat de reprodução ("tocando a mídia X"), e não só de sincronização. Disparar alerta quando a TV sincroniza mas não reproduz nada.
- **Manutenção de hardware:** reiniciar a box e coletar logs pelo painel. Monitorar espaço livre e erros do player. Deixar uma box reserva já configurada e um pareamento por QR que substitua o aparelho em minutos. Colocar um adesivo na TV com o WhatsApp da operadora, para o parceiro ter a quem chamar.
- **Servidor caseiro:** backup automático do banco, fora do celular, com teste de restauração. Ter um procedimento escrito para subir a API em outro lugar. Monitorar o próprio servidor de fora, porque hoje, se ele cair, o alerta das TVs cai junto.
- **Churn de anunciante:** relatório mensal automático por e-mail (exibições por TV e por dia, leituras do QR por link redirecionado e acessos ao diretório), enviado antes da data de renovação. Lembrete de vencimento e um campo simples de status de pagamento ligado ao bloqueio que já existe. Pré-visualização do anúncio do jeito que vai aparecer na TV e checagem de resolução e duração no upload. Sugerir trocar o criativo depois de algumas semanas no ar.

**O que cortar:** autocadastro de clientes e cobrança completa dentro do sistema. Com 10 TVs isso é distração. Um status de pagamento basta. Reavaliar se as cotações fazem sentido para o público de academias e clínicas. A prioridade única dos próximos ciclos deve ser **provar que o anúncio passou e que alguém reagiu a ele**.

**Em uma frase**, o produto deveria ser: "Seu anúncio nas TVs dos lugares do seu bairro, com um relatório mensal mostrando onde passou e quantas pessoas leram o QR." Hoje só a primeira metade é verdade.

## Perguntas que eu faria ao fundador
- O que o anunciante vê no dia 30 que o faz renovar? (/founder-consumer)
- Quanto custa cada TV instalada e quanto você cobra por mês? (/founder-cfo, /founder-pricing)
- O que o concorrente local de mídia indoor entrega como prova de veiculação? (/founder-competitors)
- Quem liga a TV todo dia no local, e o que o contrato com o parceiro diz se ela ficar desligada?
- O que acontece com as TVs e com os clientes se o celular-servidor morrer hoje à noite?
- Quantas horas por semana uma pessoa só consegue gastar com visita técnica antes de travar o crescimento?

## Se FUND IF: as condições
1. Relatório de veiculação e contagem de QR funcionando antes de cobrar o primeiro cliente.
2. Heartbeat de reprodução e alerta de "sincroniza mas não exibe" (captura de tela ou CEC) nas 10 TVs do piloto.
3. Backup automático do servidor, com restauração testada, e monitoramento externo.
4. Preço e modelo de cobrança definidos e testados com anunciantes reais (/founder-pricing), com a taxa de renovação medida ao fim do terceiro mês do piloto.

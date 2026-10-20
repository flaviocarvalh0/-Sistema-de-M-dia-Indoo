# Ata do conselho — Mídia Indoor

**Votação: 3 × "investe se" (FUND IF), nenhum "investe" puro, nenhum "passa". Nota média: 4,7 / 10.**

| Lente | Nota | Voto |
|---|---|---|
| Oferta | 5 | FUND IF |
| Monopólio | 4 | FUND IF |
| Produto | 5 | FUND IF |

Os pareceres completos estão em `founder/board/` (offers.md, monopoly.md, product.md).

Resumo em uma linha: **a tecnologia do player está acima da média para um piloto, mas o negócio
ainda não entrega ao anunciante o que justifica pagar todo mês.** Nenhuma das três lentes
acredita na renovação sem prova de veiculação.

## Riscos levantados por mais de uma lente (prioridade)

1. **O anunciante paga e não vê nada → churn no 2º–3º mês** (as três lentes, o risco nº 1
   de duas delas). Não existe registro de exibição, contagem de leituras do QR nem
   relatório. A renovação vira um ato de fé.
2. **"Online" no painel não quer dizer "exibindo" na parede** (as três). O dono do local
   desliga a TV, troca a entrada para o futebol ou baixa o brilho; a box continua
   sincronizando e o painel mostra tudo verde. Sem detectar isso, qualquer prova de
   veiculação seria falsa.
3. **Servidor caseiro sem backup e operação de uma pessoa só** (as três). Ponto único de
   falha: se o celular-servidor morrer, as TVs seguem tocando o que já têm, mas o painel, a
   sincronização **e o próprio alerta de TV fora do ar** caem juntos.
4. **Cota de 1 GB do armazenamento gratuito** (Oferta, Produto): cerca de 20 vídeos de
   50 MB esgotam a conta toda. Isso limita quantos clientes cabem.
5. **Manutenção de hardware com uma pessoa só** (as três): cada visita é cara; falta
   histórico por aparelho e um jeito de trocar uma box em minutos.
6. **Cobrança e inadimplência fora do sistema** (as três): o bloqueio existe, mas não há
   vencimento, aviso nem histórico.
7. **Preço e modelo indefinidos** (as três): sem eles não dá para saber se o repasse ao
   parceiro cabe na margem.

### Onde as lentes discordam

- **A lente de Monopólio** coloca em 1º lugar um risco que as outras quase não citam: **o
  parceiro trocar você por um operador que pague mais**. Para ela, o único ativo
  defensável é o ponto físico, e ele só é seu com contrato escrito, prazo mínimo e alguma
  exclusividade. Também pede foco num nicho (ex.: todas as academias da cidade) antes de
  expandir.
- **A lente de Oferta** acha que o anunciante para no primeiro passo ("faça um vídeo") e
  quer a produção do primeiro criativo inclusa na oferta, mais uma garantia do tipo "TV
  apagada = dia não cobrado".
- **A lente de Produto** quer **cortar** (autocadastro de clientes, cobrança completa no
  sistema, talvez as cotações) para concentrar os próximos ciclos numa única coisa: provar
  que o anúncio passou e que alguém reagiu.

## O que prever no software (consolidado, em ordem de prioridade)

> Nota de verificação: a lente de Produto pediu que o player valide período e dias de
> exibição pelo relógio local. **Isso já existe** (o filtro do dia roda na TV, no fuso do
> parceiro, mesmo offline). O que falta dessa sugestão é **conferir o relógio do aparelho**
> (alerta se a hora da TV estiver errada).

**1. Prova de veiculação (bloqueia a cobrança)**
- [ ] Log de exibição gravado no player a cada item (mídia, tela, início, duração, se
  terminou), guardado no banco local e enviado na sincronização — sobrevive a queda de
  internet.
- [ ] Relatório por cliente: exibições por TV, por dia e por faixa de horário; e-mail
  mensal automático antes da renovação.
- [ ] Separar no relatório "tocou offline" de "não tocou".
- [ ] QR rastreado: o QR aponta para um link curto da API que conta a leitura e redireciona
  (vale para link, WhatsApp, Instagram). Contar também os acessos ao diretório.

**2. "Está realmente exibindo?" (sem isso a prova é falsa)**
- [ ] Heartbeat de reprodução ("tocando a mídia X agora"), além do de sincronização.
- [ ] Alerta "sincroniza mas não exibe" (app fora de primeiro plano, fila parada).
- [ ] HDMI-CEC quando a TV suportar: ligar a TV, voltar para a entrada certa, ler o estado
  da tela; liga/desliga agendado pelo horário do local.
- [ ] Captura de tela periódica, em baixa resolução, visível no painel.
- [ ] Alerta de relógio da TV errado.
- [ ] Indicador de "tempo de tela no ar" por TV (base para uma eventual garantia).

**3. Continuidade da operação**
- [ ] Backup automático diário do banco **fora do celular**, com restauração testada.
- [ ] Monitoramento externo do próprio servidor (hoje, se ele cai, o alerta cai junto).
- [ ] Procedimento escrito para subir a API em outro lugar; migrar para hospedagem barata
  antes de passar de 10 TVs.
- [ ] Alerta de espaço do armazenamento e política de limpeza de mídias inativas.
- [ ] Painel mostrando há quanto tempo cada TV está com a grade desatualizada.

**4. Manutenção de hardware**
- [ ] Ficha por aparelho: modelo, instalação, ocorrências, visitas, tempo até voltar ao ar.
- [ ] Ações remotas pelo painel: reiniciar o app/box, coletar logs, ver espaço livre e
  erros do player.
- [ ] Troca rápida: box reserva pré-configurada e re-pareamento em minutos.
- [ ] Adesivo na TV com o WhatsApp da operadora (processo, não software).

**5. Retenção e cobrança (mínimo, sem sistema financeiro completo)**
- [ ] Status de pagamento e vencimento por cliente, aviso antes de bloquear, bloqueio
  automático após atraso (usa o bloqueio que já existe).
- [ ] Lista de "clientes em risco" (vencimento próximo, poucas leituras de QR).
- [ ] Prévia do anúncio do jeito que aparece na TV e checagem de resolução e duração no
  envio; sugestão de trocar o criativo depois de algumas semanas.

**6. Parceiros**
- [ ] Contrato do parceiro no cadastro: vigência, repasse, exclusividade de categoria,
  alerta de vencimento.
- [ ] Histórico de desempenho por ponto (horas no ar, exibições) para negociar o repasse.
- [ ] Remanejar os anúncios de um ponto perdido para outras TVs sem quebrar o contrato com
  o cliente.

## Condições do conselho (checklist para o resto do pacote)

- [ ] Prova de veiculação + contagem de QR + relatório mensal **antes de cobrar o primeiro
  anunciante** (as três lentes).
- [ ] Detecção de "online mas não exibindo" nas 10 TVs do piloto (as três).
- [ ] Backup automático com restauração testada e monitoramento externo do servidor (as três).
- [ ] Preço e modelo de cobrança definidos com dados reais → `/founder-consumer`,
  `/founder-pricing`, `/founder-cfo` (as três).
- [ ] Economia por TV (custo da box, instalação, visitas, repasse) → `/founder-cfo`.
- [ ] Contrato escrito com os parceiros do piloto, com prazo mínimo e alguma
  exclusividade (Monopólio).
- [ ] Um nicho escolhido e dominado antes de expandir (Monopólio).
- [ ] Oferta montada: produção do primeiro criativo inclusa, QR rastreado, relatório,
  diretório e uma garantia ligada ao tempo de tela no ar (Oferta) → `/founder-offer`.
- [ ] Taxa de renovação medida ao fim do 3º mês do piloto (Produto).
- [ ] Planejar a saída do armazenamento gratuito (Oferta, Produto) → `/founder-cfo`.

## A versão mais forte deste negócio

O conselho não vê força em "uma TV com anúncios". Vê força em **ser dono dos melhores pontos
de um nicho numa cidade** — por exemplo, as academias — com contrato, e vender ao comércio
do bairro um pacote fechado: **"seu anúncio nas TVs dos lugares que seu cliente frequenta,
com o vídeo feito por nós e um relatório mensal mostrando onde passou e quantas pessoas
leram o QR"**. Isso é diferente do que foi apresentado: o produto deixa de ser o espaço na
tela e passa a ser o **resultado comprovado**. O software atual já resolve bem a parte
difícil da TV (offline, atualização, alertas); o que falta é medir e mostrar. Enquanto isso
não existir, o conselho recomenda não escalar além das 10 TVs nem fixar preço.

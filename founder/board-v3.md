# Ata do conselho — Mídia Indoor, 3ª reunião

**Votação: 4 × "investe se" (FUND IF), nenhum "passa". Nota média: 5,75 / 10** (2ª reunião: 5,0).

| Lente | 1ª | 2ª | 3ª | Voto |
|---|---|---|---|---|
| Monopólio | 4 | 5 | **5** | FUND IF |
| Oferta | 5 | 6 | **7** | FUND IF |
| Produto | 5 | 6 | **7** | FUND IF |
| CFO | — | 3 | **4** | FUND IF |

Pareceres em `founder/board/v3/`. Briefing, com o status de cada item conferido no repositório,
em `founder/board/v3/brief.md`.

**Resumo em uma linha:** o fundador cumpriu quase toda a lista de **software** da 2ª ata, e as
duas lentes que cobravam isso (Oferta e Produto) subiram 1 ponto cada. Nenhum item
**comercial, contratual ou financeiro** andou, e as duas lentes que cobravam isso (Monopólio e
CFO) ficaram onde estavam, com o CFO ganhando 1 ponto só pelo teto de armazenamento. O piloto
pago continua bloqueado por coisas que não são código.

## O que o conselho reconhece como entregue (conferido)

- **Log de exibição em lote** no player, com a marca "sem conexão", envio a cada 15 min e API
  idempotente (ADR-023).
- **QR rastreado** para link, WhatsApp e Instagram, contando leituras sem guardar IP.
- **Relatório** por cliente com exibições, tempo na tela, leituras, por TV, anúncio, dia e hora
  local, mais "Copiar resumo para o WhatsApp". **PDF cortado**, como Produto pediu.
- **Relatório honesto:** avisa que prova o app, não a parede (`MedeTelaLigada = false`).
- **Heartbeat de reprodução** ("fora de exibição" após 10 min, com e-mail de parou/voltou) e
  **alerta de relógio** (ADR-024).
- **R2 com teto**: limite de 9 GB (nunca acima de 10), reserva antes da URL, inventário de hora
  em hora, alertas a 70/85/95% e barra no painel (ADR-025).
- **Contagem do diretório** (acessos, pessoas, toques) no relatório e por TV (ADR-027).

## O que continua pendente (consenso das quatro lentes)

1. **Nada foi visto numa TV de verdade.** Teste automatizado não é academia. Sem 7 dias seguidos
   numa TV, visita sem aviso a 3 pontos e queda de internet provocada, a prova é uma hipótese.
2. **Nenhum contrato, nem campo de contrato no cadastro** do parceiro (Monopólio, CFO, Produto,
   Oferta). O fosso continua sendo uma intenção.
3. **Sem preço, repasse, custo por TV, planilha ou oferta escrita** (as quatro). O produto está
   pronto antes da proposta. `founder/numbers.json` não existe.
4. **Backup do banco e restauração testada seguem fora**, o que agora é pior: o banco guarda a
   prova de veiculação. É também o requisito mais barato da lista (Monopólio, CFO, Oferta).
5. **Nenhum anunciante real leu o relatório.** Era a condição para a nota subir de verdade, e
   ela só passa de 7 depois disso.
6. **Cobrança no sistema**: nada de vencimento, atraso, aviso, inadimplência ou churn.
7. **R2 e monitoramento externo não estão ligados:** conta e bucket na Cloudflare e o teste de
   contrato continuam abertos; não há UptimeRobot.

### Riscos que a entrega criou

- **O relatório ainda pode conter prova falsa** (Oferta, Produto): TV desligada ou na entrada do
  futebol com o app em primeiro plano gera exibição que ninguém viu. O aviso é honesto, mas o
  número continua alto. CEC e captura de tela não existem.
- **O relatório não é uma entrega** (Produto, Oferta): é uma tela que o fundador abre e copia.
  Com 10 clientes funciona; com 30, vira tarefa que alguém esquece.
- **Dados de teste inflam as contagens** (Oferta, Produto, CFO): fundador, amigos e o
  anunciante-piloto escaneando o QR geram números que o primeiro cliente real não vai repetir.
- **QR de telefone e Pix não é rastreado** (Oferta): são os dois tipos que o comércio local mais
  entende; o relatório fica vazio de leituras para quem os escolher.
- **A corrida mudou de lugar** (Monopólio): quanto melhor o software, mais fácil para um
  concorrente copiá-lo e simplesmente oferecer repasse às 7 academias. O que protege é assinatura,
  não funcionalidade.
- **Duas definições de "TV no ar"** (Produto, CFO): o heartbeat (10 min, alerta) e o log
  (cobrança) convivem. A garantia precisa usar uma só, escrita num lugar só. Como o heartbeat não
  vê TV desligada, o passivo da garantia é do fundador.

### Onde as lentes discordam

- **O quanto o software entregue vale.** Oferta e Produto dão 7 ("a metade que faltava foi
  entregue na ordem certa"). Monopólio mantém 5 e CFO dá 4: para elas o que subiu foi o
  **custo de perder**, não a **proteção contra perder**.
- **A condição do 7.** Oferta diz que o 7 cai se a visita a 3 pontos contradisser o log.
  Produto aceita o aviso "não sabe se a TV estava ligada" só como transição e quer uma visita
  mensal que alimente um campo "verificado". Monopólio e CFO não acham que isso seja o
  gargalo.
- **Prazo do R2.** Produto não queria o R2 antes do 3º mês; o CFO considera o teto o item que
  tirou a franquia do caminho. Como o código já existe, a discordância se resolve ligando a
  conta (poucas horas) em vez de desfazer.

## Requisitos que restam para cobrar o primeiro anunciante

Consolidado dos quatro pareceres. A 2ª ata dizia "nenhum anunciante paga antes dos itens
obrigatórios"; **a regra continua**. Os itens de software que ela listava estão marcados.

### Software e operação

- [x] Log de exibição, QR rastreado, relatório, heartbeat e alerta de relógio (código e testes).
- [ ] **Conferência em TV real:** 7 dias seguidos em pelo menos 1 TV, visita sem aviso a 3
  pontos, e uma queda de internet (e de energia) provocada com a contagem conferida depois.
- [ ] **Desligar uma TV de propósito** e ver o alerta de "fora de exibição" chegar por e-mail.
- [ ] **Tela ligada:** CEC/captura **ou** visita mensal que preencha um campo "verificado" no
  relatório; até lá o relatório não pode ser usado como prova de garantia.
- [ ] **Backup diário do banco fora do celular, restauração feita, cronometrada e documentada.**
- [ ] **Monitoramento externo** do servidor ativo (termos do plano gratuito conferidos).
- [ ] **R2 em produção:** conta, bucket, teste de contrato rodado e a barra de armazenamento
  abaixo de 70%.
- [ ] **Mensagem mensal pronta** (3 números e um link), enviada sem abrir 10 telas.
- [ ] **Marcar dados de teste** para que não entrem na contagem do primeiro cliente real.
- [ ] **Cobrança mínima:** vencimento, pago/atrasado, aviso antes do bloqueio, inadimplência e
  churn por mês, no sistema ou em planilha.

### Comercial e contratos

- [ ] **Contratos assinados com a maioria das 7 academias** (ex.: 5), com prazo mínimo,
  exclusividade de mídia indoor no local, repasse por escrito, quem liga a TV todo dia e o que
  acontece quando ela fica desligada, e veto de concorrentes. Campo de vigência no cadastro do
  parceiro.
- [ ] **Um anunciante real lê o relatório** e diz com as próprias palavras o que entendeu; o
  fundador anota o que ele não entendeu.
- [ ] **Oferta escrita:** nome do pacote, preço, 1 vídeo e 1 revisão (preço da troca), garantia
  medida pelo heartbeat e máximo de cotas por loop.
- [ ] **Quais 2 ou 3 números o relatório promete e a meta de cada**, com uma frase de
  sugestão de troca do anúncio quando ficar abaixo.
- [ ] **Limite de cotas por observação** em 2 academias (tempo olhando a tela, anúncios
  lembrados). Até lá, bem abaixo de 60.

### Financeiro

- [ ] Planilha de entradas com valores reais e a fonte de cada um (`founder/numbers.json`,
  unidade = cota-mês), mais preço por cota e modelo escritos na proposta.
- [ ] **Break-even em cotas por TV abaixo de 50% da capacidade vendável**, teto de caixa e prazo
  de payback aceitável.
- [ ] Revisão de um contador antes da primeira fatura.

### Continua podendo esperar (até o 3º mês)

- [ ] Faixa horária, PDF, ficha por aparelho, box reserva e re-pareamento rápido (este último
  antes de passar de 7–10 TVs).
- [ ] Validação do instalador do app com device owner numa TV real.
- [ ] Taxa de renovação medida ao fim do 3º mês e uma página com o caminho de expansão.

## A recomendação do conselho

**Parar de construir e ir para a rua.** As quatro lentes chegaram, por caminhos diferentes, à mesma
conclusão: o ganho marginal de mais uma funcionalidade é pequeno, e o ganho de uma semana de
visitas é grande. O que o fundador deve fazer antes da próxima reunião, nesta ordem:

1. **Instalar 1 TV real, deixá-la 7 dias e conferir o log.** Puxar a tomada no meio de um lote e
   uma vez desligar a TV, e anotar o que o painel mostrou.
2. **Configurar o backup e restaurar uma vez**, cronometrando. É a única pendência que cabe numa
   tarde e protege todo o resto.
3. **Levar o relatório de teste a 2 academias e a 1 anunciante** e perguntar: quanto repasse,
   quanto você pagaria, o que você entendeu. Isso resolve ao mesmo tempo contrato, preço e
   a leitura do relatório.
4. **Preencher a planilha de entradas** com o que ouvir.

A versão mais forte do negócio continua a mesma da 2ª ata: **"o público fitness da cidade, com
prova"**. A diferença é que agora a prova existe como código. Falta existir como fato (uma
TV real, um anunciante real) e como contrato (as academias). A recomendação sobre preço não muda:
**não cobrar, não fixar preço e não passar de 10 TVs** enquanto os itens marcados acima
estiverem abertos. A nota deve passar de 7 na próxima reunião se a conferência em TV real e o
primeiro anunciante entrarem; passa de 8 com 5 contratos assinados e break-even calculado.

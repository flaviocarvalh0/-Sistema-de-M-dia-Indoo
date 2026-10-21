# Lente de Produto — 3ª reunião
Score: 7 (antes: 6) | Vote: FUND IF

## O que mudou
- **Entregou a metade que faltava e na ordem certa.** Log local com envio em lote, heartbeat,
  relógio e relatório por hora local reaproveitam o desenho offline-first do player, que era o
  ponto forte. A experiência do anunciante não termina mais no upload.
- **Atendeu às minhas objeções de 2ª reunião:**
  - O relatório conta **o que o log registrou** (concluído ou interrompido), nunca infere pelo
    status Online. Era a condição.
  - O **PDF foi cortado** e o formato é o resumo para WhatsApp. Um corte de verdade, numa lista
    que só crescia.
  - O aviso "ainda não sabe se a TV estava ligada" substitui o número falso. Aceito como
    solução de transição, desde que seja lido por quem recebe o relatório.
  - O relógio da TV é conferido, e o tempo na tela do heartbeat usa a hora da própria TV, o que
    evita alarme falso.
- O relatório por **hora local** responde à pergunta que eu fiz sobre quem passa e quando.

## O que ainda me preocupa
- **O caso mais comum de prova falsa continua aberto.** TV desligada ou na entrada do futebol
  com o app em primeiro plano gera exibições que ninguém viu. O aviso é honesto, mas o número
  continua alto. Sem CEC ou captura, a regra deve ser: **o número de exibições vai sempre
  acompanhado do total de horas "verificadas" e, no painel da operadora, de uma lista das TVs
  conferidas em visita**.
- **Nada foi observado numa TV de verdade.** Os testes cobrem o código; nenhum cobre uma
  academia: alguém trocando o canal, o box esquentando, uma queda de energia à noite. O
  desenho (SQLite local até o envio) deveria sobreviver a reinício, mas isso só vale depois
  de visto acontecer: puxar a tomada no meio de um lote e conferir a contagem.
- **Relatório sob demanda não é entrega.** O fundador precisa lembrar de gerar e copiar para
  cada anunciante todo mês. Com 10 clientes é viável; com 30 vira uma tarefa.
- **Muita informação no relatório** (por TV, anúncio, dia, hora, toques por contato). O
  comerciante lê três números. A tela serve ao fundador; a mensagem serve ao cliente.

## Riscos novos
- **Inflação por dados de teste** (fundador, amigos, anunciante-piloto escaneando o QR).
- **Duas lógicas de "TV no ar"** (heartbeat de 10 min para alerta; log para cobrança)
  convivem, e precisam estar escritas num lugar só para que a garantia use a certa.
- Um sinal do heartbeat conta `inactive` como visível. Em Android TV, uma sobreposição pode
  cobrir o app sem tirá-lo do primeiro plano.

## Requisitos (restam)
1. **7 dias seguidos** em 1 TV e **visita sem aviso a 3 pontos**, com resultado escrito.
2. Uma forma de verificar a tela ligada (CEC/captura) **ou** um procedimento de visita
   mensal que alimente o campo "verificado" do relatório. Sem isso, a garantia não pode usar
   o relatório como prova.
3. **Mensagem mensal pronta** com 3 números e link, enviada sem o fundador abrir 10 telas.
4. Um anunciante real lê o relatório; o fundador anota o que ele não entendeu.
5. Limite de cotas do piloto por observação em 2 academias (tempo olhando, anúncios lembrados).
6. Cortes mantidos: faixa horária e PDF só depois do 3º mês.

## Perguntas
- O que você deixa de construir agora para visitar as academias?
- Qual é o primeiro número que você mostraria e qual é a sua meta?

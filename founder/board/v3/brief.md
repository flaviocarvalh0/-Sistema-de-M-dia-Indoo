# Briefing do conselho — Mídia Indoor, 3ª reunião

Leia como um estranho: tudo o que você precisa está aqui e nos arquivos citados. Responda em **português**.

## Contexto

- 1ª reunião: 3 × "FUND IF", nota média 4,7 (`founder/board.md`).
- 2ª reunião: 4 × "FUND IF", nota média 5,0 (`founder/board-v2.md`; pareceres em
  `founder/board/v2/`). O motivo da nota baixa foi que **quase tudo era plano**.
- Ideia e restrições: `founder/idea.md`. Mercado: cidade de ~20 mil habitantes, **7 academias**,
  piloto de até 10 TVs, operado pelo fundador. **Preço, modelo, repasse e custo por TV
  continuam sem número.**

Desde a 2ª reunião (2026-10-20 a 2026-10-21) o fundador entregou código. Este briefing separa o
que **existe e foi conferido no repositório** do que **continua pendente**. Fontes:
`docs/PLANO_IMPLEMENTACAO.md`, ADR-023 a ADR-027 (`api/docs/decisions/`), código em `api/`,
`web/` e `player/`, e busca por palavra-chave no código para os itens ausentes.

## O que foi entregue (verificado)

| Item da 2ª ata | Status agora | Onde e como foi conferido |
|---|---|---|
| **Log de exibição em lote** | **Entregue no código.** Player grava cada anúncio exibido (concluído ou interrompido, com a marca "sem conexão") no SQLite, envia em lotes de até 500 a cada 15 min; API idempotente por UUID da TV; histórico sem chaves estrangeiras | ADR-023; `registrador_exibicoes.dart`; `VeiculacaoTests`, `registrador_exibicoes_test.dart` |
| **QR rastreado** (`/q/{codigo}`) | **Entregue.** Link, WhatsApp e Instagram passam pela API (302), contam leitura e leitor único por dia (hash diário, sem IP). Telefone e Pix seguem diretos ("não rastreado") | ADR-023; `LinkQrRastreado.cs` |
| **Relatório** | **Entregue como tela, não como envio.** Por cliente e período (até 93 dias): exibições, tempo na tela, leituras do QR, por TV, anúncio, dia e hora local; **"Copiar resumo para o WhatsApp"** (formato pedido por Produto). PDF não foi feito (por escolha) | `relatorio-veiculacao-page.component.ts`, `ClienteRelatorio.cs` |
| **Relatório avisa o que não mede** | **Entregue.** `MedeTelaLigada = false`: a página diz que o registro prova que o **app exibiu**, não que a TV estava ligada na entrada certa | ADR-023; `data-teste="aviso-tela-ligada"` |
| **Heartbeat de reprodução** | **Entregue.** A sincronização leva o que está tocando, primeiro plano, itens de hoje e relógio. Motivos: app fora da tela, reprodução travada, espera com anúncios válidos. 10 min seguidos = "Fora de exibição", com e-mail de "parou" e "voltou" | ADR-024; `HeartbeatTests` |
| **Alerta de relógio** | **Entregue.** Desvio acima de 5 min avisa "relógio errado" no painel | ADR-024 |
| **R2 + teto** | **Entregue no código, não ligado.** `ArmazenamentoR2` com SigV4 próprio; teto padrão 9 GB (máx. 10 GB) com reserva antes da URL, inventário de hora em hora, órfãos apagados com salvaguarda, alertas a 70/85/95%, barra no painel, cópia Supabase → R2. **A conta e o bucket na Cloudflare ainda não existem; o teste de contrato não rodou** | ADR-025; `TetoArmazenamentoTests`, `AssinaturaS3Tests`; plano: itens abertos |
| **Contagem do diretório** | **Entregue.** Acessos, pessoas por dia e toques por contato e em "Quer anunciar aqui?" (sem IP), no relatório e por TV | ADR-027; `DiretorioTests` |
| Extras | Parâmetros operacionais no painel (ADR-026); "Quer anunciar aqui?" no QR do diretório; APK por arquitetura com script que sobe a versão | plano; ADR-026 |

Volume: ~349 testes de API e ~104 do player e 4 migrations novas. Todos os itens acima passam em
teste automatizado. **Nenhum foi validado numa TV real** (ver pendências).

## O que continua pendente (verificado por busca no código e no plano)

**Prova e operação**
- **Validação em TV real**: "exibições sobem depois de uma queda de internet" está aberto no
  plano. Nenhuma visita sem aviso a 3 pontos, nenhum período de 7 dias.
- **HDMI-CEC / captura de tela**: nenhuma ocorrência no código. O relatório só avisa que não mede.
- **Relatório mensal automático**: não existe agendamento. É uma tela que o fundador abre e
  copia.
- **Backup do PostgreSQL**: só um item aberto no checklist de implantação ("aguardando servidor
  na nuvem"). Nenhuma restauração testada.
- **Monitoramento externo (UptimeRobot)**: nenhuma menção no guia de implantação.
- **R2 em produção**: conta, bucket e teste de contrato pendentes.
- **Validar o instalador do app** (device owner) numa TV real: aberto desde a rodada 2.

**Negócio**
- **Contratos com as academias**: o cadastro do parceiro **não tem campo** de vigência, repasse
  ou exclusividade. Nenhuma notícia de contrato assinado.
- **Cobrança**: nada de vencimento, pago/atrasado, aviso antes do bloqueio, inadimplência ou
  churn. Só o bloqueio manual e o limite de mídias ativas do plano.
- **Preço, repasse, custo por TV, planilha de entradas**: nenhum arquivo, nenhum número
  (`founder/numbers.json` não existe).
- **Oferta escrita** (pacote, escopo do vídeo incluso, garantia, limite de cotas): não existe.
- **Observação em 2 academias** (tempo de olhar, lembrança) para limitar as cotas: sem registro.
- **Quais números o relatório promete** e a meta: sem decisão registrada. O relatório hoje mostra
  tudo.
- **Caminho de expansão** (uma página): não existe.
- Adiados por Produto até o 3º mês, ainda não feitos: faixa horária, PDF.
- Ficha por aparelho, box reserva e re-pareamento rápido: não existem.

## Padrão que o conselho deve avaliar

Da lista de requisitos de **software** da 2ª ata, quase tudo foi entregue em 1 a 2 dias. Da lista
**comercial, contratual e financeira**, nada foi entregue ou informado. O piloto pago continua
bloqueado por itens que não são código.

## O que o fundador pediu

A nova nota do conselho, o parecer atualizado de cada lente (Monopólio, Oferta, Produto e CFO) e
os **requisitos que restam** para cobrar o primeiro anunciante.

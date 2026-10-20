# ADR-024: Heartbeat de Reprodução

## Status

Aceito

## Data da decisão

2026-10-20

## Contexto

A TV podia sincronizar de minuto em minuto e não exibir nada: o app em segundo
plano (alguém apertou "Home"), o vídeo travado no mesmo quadro, ou o loop parado
em "aguardando" com anúncios válidos para hoje. Para o painel, essa TV aparecia
**Online**. O conselho (`founder/board-v2.md`) pediu o alerta "sincroniza mas não
exibe" e a conferência do relógio da TV, porque o relatório de veiculação (ADR-023)
usa a hora da própria TV.

## Decisão

### O que a TV manda (na sincronização)

A cada sincronização, junto com os parâmetros que já existiam:

| Parâmetro | O que é |
|---|---|
| `reproducao` | `Anuncio`, `Informativo` (notícia, relógio, clima, cotações) ou `Espera` |
| `reproducaoDesde` | Quando o item atual entrou na tela (relógio da TV) |
| `duracaoItem` | Duração prevista do item, em segundos |
| `midiaAtual` | Id da mídia na tela (anúncio) |
| `primeiroPlano` | O app está visível (`inactive` conta como visível) |
| `itensHoje` | Quantos anúncios valem para hoje |
| `relogio` | A hora da TV no momento da sincronização |

Sem esses parâmetros (app antigo), nada muda: a TV não entra em "Fora de exibição".

### Como a API decide

- **Tempo na tela** = `relogio − reproducaoDesde`, os dois do relógio da TV: um
  relógio errado não gera alarme falso de travamento.
- **Motivos** (`MotivoForaDeExibicao`), em ordem:
  1. `AppForaDaTela`: `primeiroPlano = false`.
  2. `ReproducaoTravada`: fora da espera e o item está na tela há mais que a duração
     prevista + **2 min** de folga.
  3. `EsperaComAnuncios`: em espera com `itensHoje > 0`.
- O primeiro sinal grava `ForaDeExibicaoDesde`; um sinal normal limpa.
- **Fora de exibição** (`SituacaoTela.ForaDeExibicao`) depois de **10 min**
  seguidos com motivo. Offline continua tendo prioridade.
- **Relógio**: `DesvioRelogioSegundos = relogio − agora (API)`. Acima de **5 min**,
  o painel avisa "relógio errado" (o relatório usa a hora da TV).

### Alertas

O `VerificadorTelasOffline` (de 5 em 5 min) passa a mandar também "parou de exibir"
e "voltou a exibir", no mesmo e-mail e no mesmo horário dos alertas de queda
(ADR-020), uma vez por episódio (`AlertaForaDeExibicaoEm`).

### Painel

Rótulo **Fora de exibição** (amarelo) e coluna **Atenção** com o motivo e o relógio
errado.

## Alternativas consideradas

- **Captura de tela na TV:** prova melhor, mas pesa na TV fraca e exige permissão
  de captura. Fica para depois, junto com HDMI-CEC (TV desligada).
- **Endpoint próprio de heartbeat:** mais uma chamada por minuto por TV. A
  sincronização já vai de minuto em minuto; os campos vão nela.
- **Relógio da API para medir o tempo na tela:** um relógio de TV adiantado ou
  atrasado viraria travamento falso.

## Consequências

- Uma TV que só sincroniza não passa mais por saudável no painel.
- A API ainda não sabe se a TV está ligada nem na entrada HDMI certa (o
  relatório continua avisando).
- Os 10 min de tolerância deixam passar travamentos curtos; é de propósito, para
  não mandar e-mail por um vídeo que engasgou.

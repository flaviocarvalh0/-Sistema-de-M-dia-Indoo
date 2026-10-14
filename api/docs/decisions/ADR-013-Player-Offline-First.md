# ADR-013: Player das TVs Offline-First

## Status

Aceito

## Data da decisão

2026-10-13

## Contexto

As TVs ficam em academias, lanchonetes e clínicas, com Wi-Fi instável ou
compartilhado com clientes. Uma TV com tela preta ou carregando é o pior
resultado possível para o parceiro. Ao mesmo tempo, a grade muda (novas
mídias, dias da semana, período de exibição) e precisa chegar às TVs sem
ninguém mexer nelas.

Restrições:

- Vídeos de até 50 MB em aparelhos com disco e rede modestos;
- Downloads podem ser interrompidos ou corrompidos;
- A TV pode passar dias sem internet e o dia continua virando;
- As URLs de download vencem em 24 h (ADR-012).

## Decisão

O app das TVs (`player/`, Flutter para Android TV) é **offline-first**:

1. **Só toca arquivo local.** Nunca faz streaming da URL; ela serve só para
   baixar.
2. **Download seguro:** baixa para `<arquivo>.part`, calcula o SHA-256 em
   blocos e só renomeia para o nome final se bater com o `hash` da grade. Até
   3 tentativas com espera crescente.
3. **Arquivos nomeados pelo hash** (`midias/<sha256>.<ext>`): a mesma mídia em
   grades diferentes é um arquivo só, e um arquivo final só existe depois de
   conferido.
4. **Troca atômica da grade:** a grade nova fica em memória enquanto baixa; só
   quando todos os arquivos estão prontos ela entra no SQLite numa transação,
   junto da `versaoGrade`. Até lá a grade antiga continua tocando. Depois da
   troca, os arquivos que saíram são apagados.
5. **Filtro do dia no aparelho:** a API manda tudo o que ainda pode tocar
   (inclusive o que começa nos próximos dias). A TV decide, a cada item, o que
   toca hoje — dias da semana e período — usando a data no **fuso do
   parceiro**.
6. **Falhas não param a tela:** 401 apaga o token e mostra aviso de
   pareamento, mas continua tocando; 403, 429, 5xx e sem rede continuam
   tocando; URL vencida ou hash errado mantêm a grade antiga e tentam de novo
   em 1 minuto (a nova sincronização traz URLs novas).
7. **Sincronização econômica:** a TV envia a `versaoGrade` que tem; sem
   mudança, a API responde `alterada: false` sem itens.

## Justificativa

- Arquivo local é a única forma de garantir reprodução com rede ruim.
- O hash protege contra arquivo truncado ou corrompido, que travaria o player
  de vídeo.
- Troca atômica evita a TV tocar uma grade pela metade.
- Calcular "hoje" no aparelho mantém dias da semana e períodos corretos mesmo
  sem internet.

Alternativas rejeitadas:

- **Streaming direto da URL:** depende da rede a cada exibição; com o Wi-Fi
  caindo, a tela trava.
- **Cache HTTP do player de vídeo:** sem controle de quando o arquivo está
  completo nem de integridade.
- **API mandar só o que toca hoje:** a TV sem internet na virada do dia ficaria
  com a grade de ontem.

## Consequências

### Ficou mais fácil

- A TV toca por dias sem internet.
- Mudanças de grade chegam sozinhas, sem tela preta durante o download.
- Diagnóstico em campo: o botão OK do controle mostra conexão, última
  sincronização e itens de hoje.

### Ficou mais difícil

- O aparelho precisa de espaço para a grade inteira (até a cota do parceiro).
- Uma mudança só aparece na TV depois de baixar tudo e na próxima
  sincronização (padrão 5 minutos).
- A regra do filtro do dia existe em dois lugares (API e app) e precisa ficar
  igual: o contrato em `docs/CONTRATO_DISPOSITIVO.md` é a referência.

## Observações de implementação

- `player/lib/sincronizacao/` (baixador e sincronizador),
  `player/lib/reproducao/filtro_do_dia.dart`, `player/lib/dados/banco_local.dart`.
- Testes do app com API e storage falsos em `HttpServer` e SQLite real, e
  teste de ponta a ponta contra a API (`player/test/integracao_api_test.dart`).

## Referências

- [ADR-011](ADR-011-Autenticacao-Propria-dos-Dispositivos.md) — Autenticação própria dos dispositivos
- [ADR-012](ADR-012-Armazenamento-Externo-de-Midias.md) — Armazenamento externo das mídias
- `docs/CONTRATO_DISPOSITIVO.md` — contrato do app das TVs

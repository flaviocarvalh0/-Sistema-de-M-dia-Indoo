# ADR-019: Carrossel com Cursor na Fila da TV

## Status

Aceito

## Data da decisão

2026-10-15

## Contexto

Uma TV pode ter mais anúncios do que cabem numa volta (ex.: 30 anúncios de
18 s para voltas de 3 minutos, que comportam 10). Com todos os anúncios em
toda volta (ADR-016), a volta fica longa e informativos e posições fixas
demoram a voltar. Com um sorteio a cada volta, alguns anunciantes passariam
mais que outros por acaso, e a frequência prometida na venda não seria
cumprida.

A TV também precisa decidir isso sozinha, offline (ADR-013).

## Decisão

- A playlist ganha **"Tamanho da volta"** (`DuracaoMaximaVoltaSegundos`, 30 a
  3.600 s; 0 = todos em toda volta, o comportamento anterior), enviado na
  composição da sincronização.
- Com limite, a TV roda um **carrossel com cursor** (`CarrosselAnuncios`, em
  `composicao_fila.dart`):
  - os itens com posição fixa entram em toda volta, na posição deles;
  - os outros formam uma lista circular (embaralhada se a playlist embaralha,
    senão na ordem da grade);
  - cada volta puxa a partir do cursor enquanto a soma das durações couber no
    limite. Os fixos contam e os informativos não. Entra pelo menos um anúncio
    por volta;
  - no fim da lista, embaralha de novo e recomeça, sem repetir um anúncio na
    mesma volta e sem emendar o mesmo anúncio duas vezes seguidas;
  - grade nova: quem saiu sai da lista e o cursor acompanha; quem entrou vai
    para o trecho que ainda não passou neste ciclo.
- **A volta em andamento vai até o fim.** A TV publica uma situação nova a cada
  sincronização (60 s). Sem esta regra, cada sincronização remontaria a fila e
  gastaria o cursor sem exibir os anúncios separados. Quem saiu da grade é
  pulado; o resto entra na próxima volta.
- O estado (lista e cursor) fica **em memória**. Se o app reinicia, começa um
  ciclo novo, sorteado: ninguém é favorecido de forma sistemática.

## Justificativa

- Garante a mesma frequência: em N ciclos, cada anúncio passa N vezes. Nos
  testes, 200 voltas com 25 anúncios deram diferença máxima de 1 exibição.
- Não depende do servidor nem de relógio sincronizado entre TVs.

Alternativas rejeitadas:

- **Sorteio a cada volta:** a frequência fica igual só na média, com
  variação perceptível em poucas horas.
- **Montar a fila no servidor:** quebra o modo offline (ADR-016).
- **Guardar o cursor no banco local:** a TV reinicia pouco e um ciclo novo
  sorteado não favorece ninguém; fica para se a prova de veiculação exigir.

## Consequências

### Ficou mais fácil

- Vender mais anúncios por TV do que cabem numa volta, com frequência igual.
- O painel mostra em quantas voltas todos passam.

### Ficou mais difícil

- Com carrossel, mudança de grade entra no fim da volta (até o limite
  configurado), não no fim do item atual.

## Referências

- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first
- [ADR-016](ADR-016-Composicao-da-Fila-na-TV-Noticias-e-Clima.md) — Composição da fila na TV

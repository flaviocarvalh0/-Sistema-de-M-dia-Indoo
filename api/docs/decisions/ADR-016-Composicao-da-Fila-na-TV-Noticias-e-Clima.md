# ADR-016: Composição da Fila na TV, Notícias e Clima

## Status

Aceito

## Data da decisão

2026-10-14

## Contexto

A grade só com anúncios ficava monótona e repetia sempre a mesma sequência.
A operadora pediu:

- intervalos com notícias reais ("uma notícia a cada X anúncios");
- ordem dos anúncios sorteada, com posições fixas (ex.: o anúncio do
  próprio estabelecimento);
- relógio e clima na tela.

Restrições:

- **Offline-first (ADR-013):** a TV precisa continuar variando a ordem e
  mostrando notícias sem internet.
- **Detecção de mudança:** a `versaoGrade` só pode mudar quando o conteúdo
  muda; senão a TV trocaria a grade a cada sincronização.
- **Licença:** manchete e foto de portais comerciais não podem ser exibidas
  em estabelecimento sem licença.

## Decisão

- **A fila é montada na TV, a cada volta do loop** (`ComposicaoFila`). O
  servidor manda os ingredientes e as regras; a TV sorteia e intercala.
  - A composição fica na **playlist**: `noticiaACadaAnuncios` (0 = sem
    notícias, até 20), `duracaoNoticiaSegundos` (5 a 60),
    `embaralharAnuncios`, `exibirRelogio`, `exibirClima`.
  - O item da grade ganha `posicaoFixa`: não entra no sorteio.
  - A contagem de anúncios continua de uma volta para a outra; sem anúncios
    para hoje, a volta é só de notícias.
- **Notícias:**
  - Fontes RSS/Atom cadastradas pela operadora (`FonteNoticia`, Rede →
    Fontes de notícias). A migration cadastra a **Agência Brasil**
    (Creative Commons BY), com o crédito "Fonte: Agência Brasil".
  - A API lê os feeds a cada hora (`ColetorConteudoExterno`, com
    `pg_try_advisory_lock` para uma instância só) com um leitor próprio de
    RSS 2.0 e Atom (sem DTD, sem entidades externas).
  - A imagem é baixada pela API, conferida (JPEG/PNG, até 2 MB) e guardada no
    bucket em `noticias/{fonteId}/{hash}.ext`, com SHA-256, como as mídias.
    Sem imagem, a notícia vale só com o título.
  - Ficam as 30 mais recentes por fonte; as outras saem, com os arquivos.
    Notícia com mais de 48 h não vai para as TVs, e a TV a descarta sozinha.
  - A sincronização manda as 20 mais recentes das fontes ativas, com
    **versão própria** (`versaoNoticias`): a coleta de hora em hora não
    reenvia a grade de anúncios.
- **Clima:** Open-Meteo (gratuito, sem chave, CC BY com crédito na tela).
  - A API geocodifica a cidade do parceiro que hospeda a tela (aceita
    "Cidade - UF" para desempatar) e atualiza a cada 30 min
    (`PrevisaoClima`). As coordenadas só são buscadas de novo quando a
    cidade muda.
  - A sincronização manda a leitura (até 6 h) em toda resposta; a TV guarda
    a última e a mostra enquanto valer, mesmo offline.
- **Relógio:** na hora do fuso do estabelecimento, não na do aparelho.

## Justificativa

- Montar a fila no servidor quebraria o modo offline (a mesma ordem para
  sempre) e mudaria a versão da grade a cada sincronização.
- A imagem no nosso storage, com hash, reaproveita o caminho de download
  seguro das mídias e funciona offline; ler a foto direto do portal não.
- Agência Brasil é conteúdo público com licença que permite uso comercial
  com crédito; outras fontes ficam por conta e risco de quem cadastra.

Alternativas rejeitadas:

- **Embaralhar e intercalar no servidor:** os dois problemas acima.
- **G1/UOL/CNN como fonte padrão:** RSS para leitura pessoal; exibir
  manchete e foto em estabelecimento é reprodução pública sem licença.
- **Biblioteca `System.ServiceModel.Syndication`:** dependência a mais para
  pouco; o leitor próprio cobre RSS 2.0, RSS 1.0 e Atom e é testado.

## Consequências

### Ficou mais fácil

- Cada volta sai diferente, inclusive offline.
- Trocar a frequência de notícias ou desligar relógio e clima é editar a
  playlist; vale na próxima sincronização.

### Ficou mais difícil

- A API passa a precisar de saída para a internet (feeds, imagens e
  Open-Meteo). Sem ela, as fontes mostram o erro no painel e as TVs seguem
  sem notícias novas.
- As imagens das notícias ocupam o storage do plano (até 30 por fonte,
  2 MB cada no pior caso).
- O banco local da TV passou para a versão 2 (posição fixa, notícias,
  composição e clima), com migração automática.

## Referências

- [ADR-012](ADR-012-Armazenamento-Externo-de-Midias.md) — Armazenamento externo das mídias
- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first
- [ADR-015](ADR-015-Playlists-da-Operadora-e-Plano-do-Anunciante.md) — Playlists da operadora

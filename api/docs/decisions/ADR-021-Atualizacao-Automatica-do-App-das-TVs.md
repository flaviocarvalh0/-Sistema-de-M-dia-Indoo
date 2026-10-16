# ADR-021: Atualização Automática do App das TVs

## Status

Aceito

## Data da decisão

2026-10-16

## Contexto

As TVs ficam em estabelecimentos de terceiros e o app não está na Play Store
(é instalado por APK). Cada versão nova exigiria ir até cada TV com um
pendrive. Com dezenas de TVs, correções deixariam de chegar.

## Decisão

- A operadora envia o APK em **Rede → Telas → Versões do app** (`VersaoApp`):
  código (`versionCode`, o `+N` do pubspec, único), nome, notas, SHA-256 e o
  arquivo no storage (`app/{guid}.apk`, até 50 MB). A API confere a assinatura
  ZIP, o tamanho e o hash; se falhar, apaga o arquivo.
- Cada versão tem uma **distribuição**: `Suspensa` (padrão), `Teste` (só
  telas marcadas como **TV de teste**) ou `Todas`. Só versão suspensa pode
  ser excluída.
- A TV manda `versaoCodigoApp` na sincronização. A resposta traz
  `atualizacao` (código, nome, URL assinada, hash e tamanho) com a **maior
  versão liberada para ela com código maior que o seu**; senão, nulo.
- No player, `AtualizadorApp` baixa o APK com o mesmo baixador das mídias
  (hash conferido, retentativas) e chama o `PackageInstaller` pelo canal
  nativo `br.com.midiaindoor/atualizador`. Falhou? Tenta a mesma versão de
  novo só depois de 1 hora. Instalado, o Android reabre o app
  (`MY_PACKAGE_REPLACED`).
- **Sem confirmação** quando o app é *device owner*, configurado uma vez por
  TV pelo adb (ver `docs/IMPLANTACAO.md`). Sem isso, o Android mostra a
  confirmação na TV e alguém precisa apertar "Instalar" no controle.
- O painel mostra a versão de cada TV e quantas ainda estão com um app sem
  atualização automática (essas precisam de uma instalação manual, uma vez).

## Justificativa

- Reaproveita o storage, a URL assinada e o baixador já testados das mídias.
- Liberação em etapas (teste → todas) limita o estrago de uma versão ruim.
- Device owner é o caminho do Android para quiosques gerenciados sem loja.

Alternativas rejeitadas:

- **Play Store (faixa privada):** exige conta Google em cada TV e o tempo de
  revisão da loja; muitas TVs box não têm Play Store certificada.
- **MDM comercial:** custo mensal por aparelho, desnecessário no piloto.
- **Baixar e abrir o instalador por Intent:** pede confirmação sempre e não
  funciona sem alguém na frente da TV.

## Consequências

### Ficou mais fácil

- Corrigir e evoluir o player sem visitar as TVs.
- Testar uma versão numa TV antes de liberar para a rede.

### Ficou mais difícil

- O Android não volta para uma versão menor sem desinstalar: para desfazer,
  é preciso publicar uma versão nova com código maior.
- Todo APK precisa ser assinado com a mesma chave, senão o Android recusa a
  atualização. A chave de assinatura vira um segredo crítico da operadora.
- Tornar o app device owner exige a TV sem contas Google cadastradas no
  momento do comando.

## Referências

- [ADR-011](ADR-011-Autenticacao-Propria-dos-Dispositivos.md) — Autenticação dos dispositivos
- [ADR-012](ADR-012-Armazenamento-Externo-de-Midias.md) — Armazenamento externo
- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first

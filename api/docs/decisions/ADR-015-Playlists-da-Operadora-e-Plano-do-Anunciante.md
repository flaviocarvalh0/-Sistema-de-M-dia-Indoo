# ADR-015: Playlists da Operadora e Plano do Anunciante

## Status

Aceito. Substitui em parte o [ADR-014](ADR-014-Posse-das-Telas-pela-Operadora.md)
(a escolha da playlist pelo parceiro e a regra de troca de parceiro da tela).

## Data da decisão

2026-10-14

## Contexto

Depois da Entrega 1, o parceiro ainda montava playlists com as próprias
mídias e escolhia o que cada TV tocava. O negócio é outro:

- a operadora vende espaço de anúncio na rede e monta a programação de cada
  TV, misturando anúncios de vários parceiros;
- o parceiro é um **anunciante**: envia e mantém os próprios criativos;
- a TV fica num estabelecimento (o **host**), que também é um parceiro
  cadastrado e define o fuso horário da grade;
- o plano do anunciante limita quantas mídias ele pode ter no ar ao mesmo
  tempo.

## Decisão

- **Playlist é da rede.** Sai o `ParceiroId` da playlist; o nome passa a ser
  único na rede. A entidade `playlist` vai para o módulo Rede, com rotas
  `api/v1/business/rede/playlists` e permissões `Playlists.*` só na
  Operadora.
- **A operadora lê as mídias de todos os parceiros** em
  `api/v1/business/rede/midias` (somente leitura, com pré-visualização).
  A política é `Playlists.View` **e** `Parceiros.View`, e não `Midias.View`:
  essa permissão faria o painel mostrar à operadora o módulo do parceiro.
- **Grade:** item novo só entra com mídia ativa (400). Itens já na grade
  continuam se a mídia for inativada (ela só sai do loop). A mesma mídia pode
  entrar mais de uma vez; o seletor mostra "Já adicionado (N×)".
- **Tela:** a operadora escolhe o host (`ParceiroId`) e a playlist no
  cadastro. Trocar o host **não** desvincula a TV nem tira a playlist (sai o
  `Tela.TransferirPara`). O fuso continua vindo do host.
- **Plano:** `Parceiro.LimiteMidiasAtivas` (padrão 1, entre 1 e 1000),
  editado pela operadora.
  - Mídia nova nasce ativa se houver vaga e inativa se o plano estiver
    completo.
  - Ativar além do limite dá 400 com a mensagem do plano.
  - Reduzir o limite não inativa nada (direito adquirido): só impede ativar
    novas.
- **Substituir arquivo:** `PUT parceiro/midias/{id}/arquivo` troca o arquivo
  de uma mídia, que continua nas mesmas grades, ativa e com o mesmo título.
  O arquivo novo passa pela mesma conferência do envio (tipo real, tamanho,
  hash, cota descontando o arquivo antigo). O arquivo antigo sai do storage
  depois de salvar. As TVs baixam de novo porque o hash muda.
- **"Onde meus anúncios passam":** `GET parceiro/telas` lista, somente
  leitura, as telas ativas cuja playlist ativa tem uma mídia ativa do
  parceiro (nome, local, estabelecimento e conexão). Tela de outro lugar dá
  404. O parceiro não vê o nome da playlist.
- **TV e bloqueios:**
  - host bloqueado não afeta a TV: ela pareia e sincroniza normalmente;
  - anunciante bloqueado: as mídias dele saem da grade na próxima
    sincronização;
  - a sincronização só responde 403 para tela inativa.

## Justificativa

- Reflete quem vende e monta a programação (operadora) e quem paga pelo
  espaço (anunciante).
- O limite por mídias ativas é simples de explicar e de conferir num ponto
  só (`LimiteMidiasAtivas`), sem mexer no que já está no ar.
- Substituir o arquivo é o caminho natural para trocar o anúncio sem
  esbarrar no limite e sem a operadora refazer as grades.

Alternativas rejeitadas:

- **Playlist por parceiro com "mistura" feita pela operadora:** duas camadas
  de grade para a mesma TV e regras de prioridade difíceis de explicar.
- **Inativar automaticamente ao reduzir o plano:** tiraria anúncio do ar sem
  aviso.

## Consequências

### Ficou mais fácil

- A operadora monta a grade de qualquer TV com os anúncios de toda a rede.
- O anunciante troca o criativo em um passo e vê onde ele está passando.

### Ficou mais difícil

- A migration `Business_PlaylistDaOperadoraELimiteMidias` renomeia nomes de
  playlist repetidos entre parceiros antes de criar o índice único.
- O `studio-indoor.sql` precisa remover do Proprietário de Parceiro as
  permissões de playlist e os itens de menu antigos.

## Referências

- [ADR-011](ADR-011-Autenticacao-Propria-dos-Dispositivos.md) — Autenticação própria dos dispositivos
- [ADR-012](ADR-012-Armazenamento-Externo-de-Midias.md) — Armazenamento externo das mídias
- [ADR-014](ADR-014-Posse-das-Telas-pela-Operadora.md) — Posse das telas pela operadora

# Histórico de versões — Mídia Indoor

Este arquivo é a fonte: o build o carimba dentro da aplicação, e é ele que a
tela de notas de versão (`/versoes`, atrás do login) mostra. Escreva pensando
em quem vai ler depois, não em quem escreveu o código: "a grade passou a
mostrar a duração do ciclo" vale mais que "ajuste no `computed`".

O cabeçalho só precisa conter o número da versão. `## [0.1.0] - 2026-10-13` e
`## 0.1.0` funcionam igual.

---

## [0.1.0] - 2026-10-13

Primeira versão do painel.

- **Parceiros** (operadora): cadastro com o login do responsável, espaço de
  armazenamento, bloqueio e desbloqueio.
- **Mídias**: envio de vídeos e imagens direto para o armazenamento, com
  barra de progresso e conferência do arquivo; título, duração, período de
  exibição, pré-visualização e o espaço usado.
- **Playlists**: grade montada arrastando os itens, dias da semana de cada um
  e duplicação com a grade.
- **Telas**: código de pareamento em destaque, playlist de cada TV e a
  conexão (aguardando pareamento, online ou offline).

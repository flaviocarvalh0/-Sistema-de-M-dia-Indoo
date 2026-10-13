# Sistema de Mídia Indoor

Digital signage para parceiros (academias, lanchonetes, clínicas): o parceiro
cadastra vídeos e imagens, monta playlists e escolhe o que cada TV exibe. As
TVs tocam em loop a partir de arquivos baixados — sem internet, continuam no
que já têm.

| Pasta | Conteúdo | Base |
|---|---|---|
| [`api/`](api/README.md) | Backend .NET 9 | Atlas Platform (`v0.4.0-alpha.1`) |
| `web/` | Painel administrativo Angular | `@atlas/framework` *(etapa 6)* |
| `player/` | App das TVs (Android TV) | Flutter *(etapa 7)* |

- [Plano de implementação](docs/PLANO_IMPLEMENTACAO.md)
- [Modelo de dados](api/docs/MODELO_DADOS.md)

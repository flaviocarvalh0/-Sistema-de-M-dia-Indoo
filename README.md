# Sistema de Mídia Indoor

Digital signage para parceiros (academias, lanchonetes, clínicas): o parceiro
cadastra vídeos e imagens, monta playlists e escolhe o que cada TV exibe. As
TVs tocam em loop a partir de arquivos baixados — sem internet, continuam no
que já têm.

| Pasta | Conteúdo | Base |
|---|---|---|
| [`api/`](api/README.md) | Backend .NET 9 | Atlas Platform (`v0.4.0-alpha.1`) |
| [`web/`](web/README.md) | Painel administrativo Angular | `@atlas/framework` |
| [`player/`](player/README.md) | App das TVs (Android TV) | Flutter |

- [Plano de implementação](docs/PLANO_IMPLEMENTACAO.md)
- [Como testar na sua máquina](docs/COMO_TESTAR.md)
- [Contrato do app das TVs](docs/CONTRATO_DISPOSITIVO.md)
- [Modelo de dados](api/docs/MODELO_DADOS.md)

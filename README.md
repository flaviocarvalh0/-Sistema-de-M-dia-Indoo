# Sistema de Mídia Indoor

Digital signage em rede: a operadora instala TVs nos **parceiros**
(academias, lanchonetes, clínicas — o local cede o espaço e não usa o
sistema) e monta as playlists com os anúncios dos **clientes**, que enviam os
próprios vídeos e imagens pelo painel. As TVs tocam em loop a partir de
arquivos baixados — sem internet, continuam no que já têm.

| Pasta | Conteúdo | Base |
|---|---|---|
| [`api/`](api/README.md) | Backend .NET 9 | Atlas Platform (`v0.4.0-alpha.1`) |
| [`web/`](web/README.md) | Painel administrativo Angular | `@atlas/framework` |
| [`player/`](player/README.md) | App das TVs (Android TV) | Flutter |

- [Catálogo de funcionalidades](docs/FUNCIONALIDADES.md)
- [Guia de implantação](docs/IMPLANTACAO.md)
- [Plano de implementação](docs/PLANO_IMPLEMENTACAO.md)
- [Como testar na sua máquina](docs/COMO_TESTAR.md)
- [Contrato do app das TVs](docs/CONTRATO_DISPOSITIVO.md)
- [Modelo de dados](api/docs/MODELO_DADOS.md)
- [Decisões de arquitetura (ADRs)](api/docs/decisions/README.md)

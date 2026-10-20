# Registros de Decisões Arquiteturais — Atlas

Este diretório reúne os *Architecture Decision Records* (ADRs) da plataforma Atlas.

Cada ADR registra uma decisão arquitetural relevante, seu contexto, as alternativas consideradas e as consequências aceitas. Os ADRs representam o histórico das decisões; por isso, uma decisão aceita não deve ser reescrita para ocultar mudanças posteriores. Quando uma decisão for substituída, um novo ADR deverá ser criado e o anterior marcado como **Superado por**.

## Status permitidos

- **Proposto:** decisão em avaliação;
- **Aceito:** decisão aprovada e vigente;
- **Rejeitado:** alternativa analisada e não adotada;
- **Superado:** decisão substituída por outro ADR.

## Índice

| ADR | Título | Status |
|---|---|---|
| [ADR-001](ADR-001-Uso-EF-Core-Na-Camada-de-Aplicacao.md) | Uso do EF Core na Camada de Aplicação | Aceito |
| [ADR-002](ADR-002-Referencia-MediatR-Contracts-No-Dominio.md) | Referência do MediatR.Contracts no Domínio | Aceito |
| [ADR-003](ADR-003-Segregacao-Entre-Plataforma-e-Negocio.md) | Segregação Arquitetural entre Plataforma e Negócio | Aceito |
| [ADR-004](ADR-004-Convencoes-de-Idioma-e-Nomenclatura.md) | Convenções de Idioma e Nomenclatura | Aceito |
| [ADR-005](ADR-005-Estrategia-de-Schemas-No-PostgreSQL.md) | Estratégia de Schemas no PostgreSQL | Aceito |
| [ADR-006](ADR-006-Contexto-Unico-e-Migrations-Unificadas.md) | Contexto Único e Migrations Unificadas | Aceito |
| [ADR-007](ADR-007-Plataforma-Base-e-Evolucao-Dos-Produtos.md) | Plataforma-base e Evolução dos Produtos de Cliente | Aceito |
| [ADR-008](ADR-008-Gestao-de-Configuracoes-e-Segredos.md) | Gestão de Configurações e Segredos | Aceito |
| [ADR-009](ADR-009-Estrategia-de-Versionamento-Historico-de-Entidades.md) | Estratégia de Versionamento Histórico de Entidades | Aceito |
| [ADR-010](ADR-010-Estrategia-de-Mensagens-e-Entrega-de-Emails.md) | Estratégia de Mensagens e Entrega de E-mails | Aceito |
| [ADR-011](ADR-011-Autenticacao-Propria-dos-Dispositivos.md) | Autenticação Própria dos Dispositivos (TVs) | Aceito |
| [ADR-012](ADR-012-Armazenamento-Externo-de-Midias.md) | Armazenamento Externo das Mídias com URLs Assinadas | Aceito |
| [ADR-013](ADR-013-Player-Offline-First.md) | Player das TVs Offline-First | Aceito |
| [ADR-014](ADR-014-Posse-das-Telas-pela-Operadora.md) | Posse das Telas pela Operadora | Aceito (em parte substituído pelo 015) |
| [ADR-015](ADR-015-Playlists-da-Operadora-e-Plano-do-Anunciante.md) | Playlists da Operadora e Plano do Anunciante | Aceito |
| [ADR-016](ADR-016-Composicao-da-Fila-na-TV-Noticias-e-Clima.md) | Composição da Fila na TV, Notícias e Clima | Aceito |
| [ADR-017](ADR-017-QR-Code-nos-Anuncios.md) | QR Code nos Anúncios | Aceito |
| [ADR-018](ADR-018-Diretorio-de-Anunciantes-da-TV.md) | Diretório de Anunciantes da TV | Aceito |
| [ADR-019](ADR-019-Carrossel-com-Cursor.md) | Carrossel com Cursor na Fila da TV | Aceito |
| [ADR-020](ADR-020-Aprovacao-de-Troca-e-Alerta-de-TV-Offline.md) | Aprovação da Troca de Arquivo e Alerta de TV Fora do Ar | Aceito |
| [ADR-021](ADR-021-Atualizacao-Automatica-do-App-das-TVs.md) | Atualização Automática do App das TVs | Aceito |
| [ADR-022](ADR-022-Parceiro-e-Cliente.md) | Parceiro (Local da TV) e Cliente (Anunciante) | Aceito |
| [ADR-023](ADR-023-Prova-de-Veiculacao-e-QR-Rastreado.md) | Prova de Veiculação e QR Rastreado | Aceito |
| [ADR-024](ADR-024-Heartbeat-de-Reproducao.md) | Heartbeat de Reprodução | Aceito |
| [ADR-025](ADR-025-Cloudflare-R2-e-Teto-de-Armazenamento.md) | Cloudflare R2 e Teto de 10 GB | Aceito |
| [ADR-026](ADR-026-Parametros-Operacionais-no-Painel.md) | Parâmetros Operacionais no Painel | Aceito |
## Como criar um ADR

1. Copie o arquivo [`ADR-TEMPLATE.md`](ADR-TEMPLATE.md).
2. Utilize o próximo número sequencial disponível.
3. Escreva uma decisão autocontida, com contexto factual.
4. Registre alternativas e consequências reais.
5. Submeta o ADR à revisão da equipe.
6. Atualize este índice após sua aprovação.

## Origem

Os ADRs 001 e 002 foram originalmente documentados por Jason Taylor no projeto *Clean Architecture Solution Template*. A Atlas adotou e traduziu essas decisões, preservando os créditos e distinguindo a data original da data de adoção.

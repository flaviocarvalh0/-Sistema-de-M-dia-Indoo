# ADR-003: Segregação Arquitetural entre Plataforma e Negócio

## Status

Aceito

## Data da decisão

2026-07-17

## Contexto

A Atlas deverá servir como base reutilizável para aplicações de clientes distintos. Cada aplicação será composta por recursos estruturais da plataforma e por funcionalidades pertencentes ao domínio específico do cliente.

Sem uma fronteira explícita, regras de clientes podem infiltrar-se na plataforma, componentes reutilizáveis podem tornar-se dependentes de um produto específico e a manutenção de múltiplas aplicações pode produzir versões incompatíveis da mesma funcionalidade.

A solução atual utiliza projetos por camada, como `Atlas.Domain`, `Atlas.Application`, `Atlas.Infrastructure` e `Atlas.Web`. A separação entre plataforma e negócio deverá existir transversalmente nessas camadas.

## Decisão

A solução Atlas será organizada em duas fronteiras principais:

1. `Platform`: funcionalidades estruturais, reutilizáveis e independentes de clientes;
2. `Business`: entidades, regras, processos e casos de uso pertencentes ao domínio da aplicação cliente.

A fronteira será expressa inicialmente por pastas, namespaces, configurações e convenções de dependência dentro dos projetos existentes.

### Plataforma

Componentes da plataforma deverão permanecer sob raízes como:

```text
Atlas.Domain.Platform
Atlas.Application.Platform
Atlas.Infrastructure.Platform
Atlas.Web.Endpoints.Platform
```

Exemplos de responsabilidades da plataforma:

- Autenticação e autorização;
- Usuários, grupos, papéis e permissões;
- Sessões;
- Parâmetros de sistema;
- Módulos e menus;
- Metadados;
- Comunicação e histórico de e-mails;
- Observabilidade e sanitização de logs;
- Infraestrutura técnica reutilizável.

### Negócio

Componentes específicos de uma aplicação cliente deverão permanecer sob raízes como:

```text
Atlas.Domain.Business
Atlas.Application.Business
Atlas.Infrastructure.Business
Atlas.Web.Endpoints.Business
```

A área de negócio poderá conter:

- Conceitos compartilhados por mais de um módulo;
- Módulos ou contextos específicos;
- Casos de uso e integrações particulares do produto.

### Direção das dependências

Serão observadas as seguintes regras:

```text
Business → contratos públicos de Platform
Platform -X→ Business
Módulo específico → negócio compartilhado
Negócio compartilhado -X→ módulo específico
```

A plataforma não poderá referenciar entidades, regras, serviços ou integrações exclusivos de um cliente.

O domínio de negócio não deverá depender diretamente de implementações de autenticação, SMTP, provedores externos, Entity Framework Core ou outros componentes concretos de infraestrutura.

### Organização física inicial

A decisão não exige a criação imediata de um projeto `.csproj` para cada módulo. O isolamento será estabelecido inicialmente por pastas e namespaces.

A extração para projetos separados poderá ser considerada quando um módulo apresentar:

- Ciclo de vida independente;
- Volume elevado de código;
- Necessidade de distribuição própria;
- Equipe responsável independente;
- Dependências técnicas particulares;
- Potencial de reutilização fora da aplicação atual.

## Justificativa

### Alternativa: manter apenas a separação por camadas

Foi rejeitada porque `Domain`, `Application` e `Infrastructure` indicam responsabilidade técnica, mas não distinguem o que é reutilizável do que pertence a um cliente.

### Alternativa: criar um projeto para cada módulo desde o início

Foi rejeitada por introduzir complexidade prematura de referências, composição, injeção de dependência e manutenção da solução.

### Alternativa: permitir que cada cliente altere livremente a plataforma copiada

Foi rejeitada porque produziria deriva entre clientes, dificultaria correções de segurança e impediria identificar claramente a versão da plataforma utilizada.

## Consequências

### Ficou mais fácil

- Identificar a responsabilidade de cada componente;
- Evitar dependências indevidas da plataforma em regras de clientes;
- Revisar a arquitetura durante code reviews;
- Extrair componentes reutilizáveis futuramente;
- Iniciar novos produtos com uma base consistente;
- Organizar módulos sem criar projetos adicionais prematuramente.

### Ficou mais difícil

- Cada novo conceito deverá ser classificado antes da implementação;
- Componentes aparentemente genéricos exigirão análise para determinar se são realmente compartilháveis;
- A equipe deverá fiscalizar dependências de sentido inverso;
- Refatorações poderão ser necessárias quando uma responsabilidade for classificada incorretamente.

## Relações

- Complementado pelo [ADR-004](ADR-004-Convencoes-de-Idioma-e-Nomenclatura.md);
- Complementado pelo [ADR-005](ADR-005-Estrategia-de-Schemas-No-PostgreSQL.md);
- Complementado pelo [ADR-007](ADR-007-Plataforma-Base-e-Evolucao-Dos-Produtos.md).

# ADR-001: Uso do EF Core na Camada de Aplicação

## Status

Aceito

## Data da decisão original

2024-02-29

## Data de adoção no Atlas

2026-06-28

## Contexto

A camada de Aplicação precisa ler e escrever dados. A Clean Architecture restringe as camadas internas de dependerem das camadas externas, e frameworks como ORMs geralmente são colocados na camada de Infraestrutura. A questão é se a camada de Aplicação deve acessar o EF Core diretamente por meio de uma interface que ela mesma possui, ou se uma camada de repositório deve se posicionar entre elas.

## Decisão

A camada de Aplicação define o `IApplicationDbContext`, uma interface que expõe propriedades do tipo `DbSet<T>`, e a utiliza diretamente nos manipuladores de comandos e consultas. O `ApplicationDbContext`, localizado na camada de Infraestrutura, implementa essa interface.

Nenhuma camada de repositório genérica será colocada entre a camada de Aplicação e o EF Core apenas com a finalidade de ocultar o ORM.

## Justificativa

### A inversão de dependência é respeitada

O `IApplicationDbContext` é definido na camada de Aplicação. O `ApplicationDbContext`, na Infraestrutura, o implementa. A direção da dependência continua apontando para dentro: a Infraestrutura depende da Aplicação, e não o contrário.

A camada de Aplicação assume uma dependência de compilação das abstrações do EF Core, como `DbSet<T>` e `IQueryable<T>`, mas não conhece o `DbContext` concreto, o provedor de banco de dados ou tipos pertencentes à Infraestrutura.

### Repositórios genéricos adicionariam indireção sem abstração relevante

Uma camada de repositório que apenas encapsulasse operações do `DbContext` não removeria o acoplamento conceitual com o EF Core. Ela tenderia a reproduzir recursos do ORM por meio de métodos customizados, aumentando a complexidade e reduzindo a legibilidade do fluxo.

O acesso por `IApplicationDbContext` torna explícito o mecanismo utilizado e preserva o poder de projeção e composição de consultas do EF Core.

### Testes devem validar o provedor real

Consultas, constraints, índices, transações e comportamentos específicos do provedor não são representados adequadamente por mocks ou provedores simplificados. Testes funcionais deverão utilizar um banco de dados compatível com o ambiente real.

Testes unitários de regras puras de domínio e validações não necessitam de banco de dados.

### A substituição do ORM não é um requisito atual

Projetar uma abstração adicional para uma possível troca futura de ORM imporia complexidade imediata para um cenário não previsto. Caso essa necessidade surja, a arquitetura será reavaliada por meio de um novo ADR.

## Consequências

### Ficou mais fácil

- Utilizar projeções, filtros, includes e demais recursos do EF Core diretamente nos handlers;
- Manter um fluxo de acesso a dados simples: handler → `IApplicationDbContext` → `ApplicationDbContext`;
- Evitar a criação e manutenção de repositórios genéricos que apenas repitam a API do ORM;
- Evoluir consultas sem ampliar continuamente interfaces artificiais.

### Ficou mais difícil

- A camada de Aplicação possui referência ao pacote `Microsoft.EntityFrameworkCore`;
- A eventual substituição do ORM exigirá alterações nos handlers;
- Testes funcionais deverão usar um banco de dados real ou compatível com o provedor adotado.

## Quando esta decisão não se aplica

Esta decisão não impede o uso de repositórios orientados a agregados em contextos que adotem Domain-Driven Design com modelo de domínio rico.

Nesse cenário, o repositório representa um conceito do domínio e expõe operações baseadas na linguagem do negócio, em vez de apenas encapsular mecanicamente o `DbContext`.

---

## Créditos de origem

Esta decisão foi originalmente documentada em inglês por **Jason Taylor** no projeto *Clean Architecture Solution Template*. O conteúdo foi traduzido, adaptado e adotado pela plataforma Atlas em 2026-06-28.

- Projeto original: [https://github.com/jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture)

# ADR-005: Estratégia de Schemas no PostgreSQL

## Status

Aceito

## Data da decisão

2026-07-17

## Contexto

A Atlas utilizará PostgreSQL e deverá hospedar, no mesmo banco de uma aplicação, estruturas da plataforma, conceitos de negócio compartilhados e módulos específicos.

A utilização indiscriminada do schema `public` ou apenas de prefixos em nomes de tabelas reduziria a clareza das fronteiras, aumentaria o risco de colisões e dificultaria administração, consultas e concessão de permissões.

## Decisão

Schemas do PostgreSQL serão utilizados como mecanismo principal de segregação lógica dos dados.

### Schema reservado da plataforma

Entidades estruturais reutilizáveis serão armazenadas no schema:

```text
platform
```

Exemplos:

```text
platform.system_parameter
platform.system_module
platform.system_menu_item
platform.email_message
platform.smtp_configuration
platform.user_session
```

Entidades do ASP.NET Core Identity poderão permanecer com o mapeamento atual durante a transição. Qualquer mudança de schema ou nomenclatura dessas tabelas exigirá migration própria e análise de compatibilidade.

### Negócio compartilhado

Quando uma aplicação possuir conceitos de negócio realmente compartilhados entre módulos, poderá utilizar:

```text
business
```

Exemplos genéricos:

```text
business.empresa
business.endereco
business.contato
```

Uma entidade só deverá ser considerada compartilhada quando possuir:

- Significado consistente entre os módulos consumidores;
- Identidade e ciclo de vida próprios;
- Ausência de propriedades exclusivas de um único contexto;
- Benefício concreto em manter uma representação única.

### Módulos específicos

Cada módulo poderá utilizar schema próprio em `snake_case`.

Exemplos genéricos:

```text
financeiro.conta
financeiro.movimentacao
faturamento.nota_fiscal
crm.oportunidade
```

O nome e a existência de cada módulo serão decisões do produto cliente, não da plataforma-base.

### Prefixos

Não serão utilizados prefixos redundantes quando o schema já identificar a responsabilidade.

Será utilizado:

```text
financeiro.conta
financeiro.movimentacao
```

Não será utilizado:

```text
financeiro.fin_conta
financeiro.fin_movimentacao
```

### Schema `public`

Novas tabelas da aplicação não deverão ser criadas no schema `public`, salvo exceção explicitamente documentada.

### Qualificação explícita

Mapeamentos do Entity Framework Core e consultas SQL manuais deverão qualificar explicitamente o schema.

```csharp
builder.ToTable("system_parameter", "platform");
```

```sql
select *
from platform.system_parameter;
```

A aplicação não deverá depender de um `search_path` amplo para resolver objetos de forma implícita.

### Migrations

As migrations deverão:

- Criar os schemas necessários antes dos objetos dependentes;
- Qualificar tabelas, índices, chaves e constraints;
- Respeitar dependências entre schemas;
- Utilizar os prefixos `Platform_`, `Business_` ou `Shared_` definidos no ADR-004.

## Justificativa

### Alternativa: utilizar apenas o schema `public`

Foi rejeitada porque mistura objetos de responsabilidades distintas e reduz a capacidade de organização e administração.

### Alternativa: utilizar apenas prefixos

Foi rejeitada porque prefixos não constituem namespaces reais e geram nomes longos e repetitivos.

### Alternativa: utilizar schema e prefixo simultaneamente

Foi rejeitada por redundância.

### Alternativa: criar schemas por camada técnica

Schemas como `domain`, `application` e `infrastructure` foram rejeitados porque o banco deve refletir responsabilidades sobre dados, e não a organização técnica do código.

## Consequências

### Ficou mais fácil

- Identificar a responsabilidade de cada tabela;
- Evitar colisões de nomes;
- Organizar consultas e administração do banco;
- Conceder permissões por schema;
- Preparar limites que poderão apoiar extrações futuras;
- Manter nomes de tabelas mais curtos dentro de cada contexto.

### Ficou mais difícil

- Configurações do EF Core precisarão indicar schemas corretamente;
- Consultas SQL manuais deverão qualificar objetos;
- Migrations deverão criar schemas e respeitar sua ordem;
- Relacionamentos entre schemas exigirão atenção explícita;
- Mudanças futuras em tabelas existentes poderão exigir migrations cuidadosas.

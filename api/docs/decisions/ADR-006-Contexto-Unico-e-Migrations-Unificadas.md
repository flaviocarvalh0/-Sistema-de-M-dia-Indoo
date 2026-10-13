# ADR-006: Contexto Único e Migrations Unificadas

## Status

Aceito

## Data da decisão

2026-07-17

## Contexto

A aplicação final combina entidades da plataforma e entidades do negócio do cliente. Atualmente, ambas participam do mesmo modelo relacional, compartilham transações e são persistidas pelo mesmo `ApplicationDbContext`.

Separar imediatamente `PlatformDbContext` e `BusinessDbContext` introduziria duas unidades de trabalho, coordenação transacional, múltiplos snapshots e maior complexidade para relacionamentos entre entidades.

Também é necessário decidir se plataforma e negócio devem possuir históricos de migrations independentes.

## Decisão

Enquanto plataforma e negócio participarem do mesmo modelo relacional, será utilizado:

- Um único `ApplicationDbContext`;
- Um único `ModelSnapshot`;
- Uma única sequência global de migrations;
- Uma única tabela `__EFMigrationsHistory`.

A distinção da origem das alterações será feita pelos nomes das migrations:

```text
Platform_*
Business_*
Shared_*
```

A primeira migration da plataforma-base será:

```text
Platform_CriarEstruturaInicial
```

A primeira migration de um produto cliente deverá representar apenas as estruturas de negócio adicionadas após a adoção da base, por exemplo:

```text
Business_AdicionarEmpresas
```

### Responsabilidade pelas migrations

Enquanto a solução estiver integrada, o produto final será responsável pelas migrations do modelo combinado:

```text
Modelo da plataforma
+
Modelo específico do cliente
=
Modelo final da aplicação
```

### Alterações de schema

A equipe deverá alterar primeiro:

1. Entidades e modelos;
2. Configurações do EF Core;
3. Código relacionado;
4. Migration gerada;
5. Banco local por meio da migration.

Alterações manuais no banco antes do modelo não serão o fluxo padrão.

### Produção

Arquivos C# de migrations permanecerão no repositório.

Para homologação e produção, a forma inicial de implantação será um script SQL revisado, gerado a partir das migrations versionadas. A mesma sequência testada no desenvolvimento deverá ser utilizada nos demais ambientes.

### Recriação do banco

`EnsureDeleted` e `EnsureCreated` não serão utilizados no fluxo normal da aplicação após a adoção das migrations.

Um reset destrutivo poderá continuar existindo somente como ferramenta explícita para desenvolvimento, testes ou demonstrações, protegido contra execução fora desses ambientes.

### Seeds

Seeds continuarão existindo, mas deverão ser idempotentes e independentes da recriação do banco.

## Justificativa

### Alternativa: dois DbContexts desde o início

Foi rejeitada devido à complexidade transacional, aos relacionamentos entre fronteiras e à ausência de necessidade operacional atual.

### Alternativa: dois históricos de migrations para o mesmo contexto

Foi rejeitada porque um único modelo possui um único snapshot coerente e uma ordem global de alterações.

### Alternativa: tabela adicional de versionamento do banco

Foi rejeitada como mecanismo de controle estrutural porque o EF Core já mantém `__EFMigrationsHistory`.

Uma tabela de histórico de releases poderá ser criada futuramente para auditoria de deployments, mas não substituirá o histórico do EF Core.

### Alternativa: alterar o banco manualmente e adaptar o código

Foi rejeitada porque reduz a reprodutibilidade, dificulta revisão, gera deriva entre ambientes e impede que o histórico completo seja reconstruído pelo repositório.

## Consequências

### Ficou mais fácil

- Manter uma única unidade de trabalho e transação;
- Gerenciar relacionamentos entre plataforma e negócio;
- Reproduzir o banco a partir do código;
- Saber exatamente quais migrations foram aplicadas;
- Testar a mesma evolução em desenvolvimento, homologação e produção;
- Evitar snapshots concorrentes.

### Ficou mais difícil

- Plataforma e negócio compartilham uma ordem global de migrations;
- O produto final precisa avaliar impactos de atualizações da plataforma no modelo combinado;
- Conflitos de migrations poderão ocorrer quando branches concorrentes alterarem o modelo;
- A equipe precisará aprender a revisar e implantar migrations.

## Critério de revisão

A separação em contextos independentes somente deverá ser reconsiderada quando houver necessidade concreta de:

- Bancos ou ciclos de implantação independentes;
- Isolamento transacional;
- Equipes e releases autônomos;
- Distribuição física distinta;
- Limites claros que não exijam relacionamentos diretos frequentes.

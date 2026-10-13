# ADR-009: Estratégia de Versionamento Histórico de Entidades

## Status

Aceito

## Data da decisão

2026-07-28

## Contexto

A plataforma Atlas permite configurar, por meio de `EntityMetadata`, comportamentos associados às entidades sem necessidade de recompilação da aplicação consumidora. Entre esses comportamentos existe a propriedade `EnableHistory`, responsável por indicar se uma entidade deve manter um histórico operacional de seus registros.

Esse histórico possui finalidade diferente de uma trilha de auditoria.

O histórico operacional deve representar o estado completo de um registro após cada criação ou atualização bem-sucedida, permitindo que o front-end apresente a evolução do registro, o usuário responsável pela alteração, a data da modificação e a comparação entre valores anteriores e atuais.

A trilha de auditoria, por outro lado, registra eventos como criação, alteração e exclusão para fins de rastreabilidade, podendo permanecer mesmo quando o registro de negócio deixa de existir. Ela não faz parte desta decisão.

A solução precisa atender aos seguintes requisitos:

- Cada entidade com histórico habilitado deve possuir uma tabela histórica própria;
- Cada `INSERT` ou `UPDATE` bem-sucedido deve gerar um snapshot completo do estado persistido;
- O histórico deve ser excluído quando o registro principal for excluído;
- A captura deve ocorrer de forma atômica com a operação principal;
- O mecanismo deve funcionar para entidades da plataforma e de negócio que estejam registradas no `EntityMetadata` e com `EnableHistory = true`;
- O mecanismo não deve exigir a criação manual de uma entidade C# histórica para cada entidade versionada;
- A infraestrutura deve ser criada e mantida pelo backend;
- O controle de concorrência otimista baseado em `xmin` deve continuar funcionando sem ser persistido no histórico;
- A habilitação do histórico não deve criar versões retroativas para registros existentes;
- A desabilitação do histórico não deve apagar tabelas, funções, triggers ou snapshots existentes;
- Quando o histórico for reabilitado, a captura deve continuar a partir das próximas operações.

## Decisão

A Atlas adotará um engenho híbrido de versionamento histórico:

- O C# será responsável por descobrir entidades elegíveis, ler os metadados do Entity Framework Core, provisionar e sincronizar a infraestrutura histórica e consultar os snapshots;
- O PostgreSQL será responsável por capturar os snapshots por meio de triggers executadas após operações de inclusão e atualização.

### Tabela histórica por entidade

Cada entidade versionada possuirá uma tabela histórica física própria no schema:

```text
history
```

O nome da tabela histórica deverá ser gerado de forma determinística a partir do schema e da tabela de origem.

Exemplos:

```text
platform.system_parameter
history.platform_system_parameter_history

business.empresa
history.business_empresa_history
```

Cada tabela histórica conterá:

- Uma chave primária técnica própria, como `history_id`;
- A chave do registro de origem, como `record_id`;
- Uma data técnica de materialização da versão, como `history_created_at`;
- As colunas escalares persistidas da tabela de origem, exceto colunas explicitamente ignoradas pelo engenho;
- Uma chave estrangeira para o registro principal com `ON DELETE CASCADE`.

A shadow property associada ao `xmin` não será copiada para a tabela histórica nesta versão.

### Captura por trigger

O engenho criará uma função e uma trigger por tabela versionada.

A trigger será executada:

```text
AFTER INSERT OR UPDATE
```

A função da trigger deverá:

1. Consultar o `EntityMetadata` correspondente à tabela de origem;
2. Verificar o valor atual de `EnableHistory`;
3. Encerrar sem gravar snapshot quando o histórico estiver desabilitado;
4. Inserir o estado completo de `NEW` na tabela histórica quando o histórico estiver habilitado;
5. Retornar `NEW`.

A trigger permanecerá instalada quando `EnableHistory` for alterado para `false`. A interrupção da captura será controlada pela consulta ao metadado, sem remoção da infraestrutura existente.

### Autoria e datas

As entidades versionadas já possuem os campos herdados de `BaseAuditableEntity`:

```text
Created
CreatedBy
LastModified
LastModifiedBy
```

O `AuditableEntityInterceptor` continuará preenchendo esses valores antes da execução do comando SQL.

A trigger copiará esses campos para o snapshot. Não será criado, nesta versão, um contexto adicional de usuário para a trigger nem uma coluna específica para identificar o criador da linha histórica.

A coluna técnica `history_created_at` poderá ser mantida para registrar quando o PostgreSQL materializou o snapshot.

### Habilitação e desabilitação

O valor inicial de `EnableHistory` será definido pelo `MetadataConfigurationAttribute` e persistido em `EntityMetadata`.

Quando uma entidade estiver com histórico habilitado e sua infraestrutura ainda não existir, o backend deverá provisionar:

1. O schema `history`, se necessário;
2. A tabela histórica;
3. A chave primária técnica;
4. A chave estrangeira com `ON DELETE CASCADE`;
5. Os índices necessários;
6. A função da trigger;
7. A trigger.

Não será criado baseline automático para registros existentes. A primeira versão será produzida pelo primeiro `INSERT` ou `UPDATE` ocorrido após a infraestrutura estar disponível e o histórico estar habilitado.

Quando o histórico for desabilitado:

- Os snapshots existentes serão preservados;
- A tabela histórica será preservada;
- A função será preservada;
- A trigger será preservada;
- Novos snapshots não serão inseridos enquanto `EnableHistory = false`.

### Exclusão

A exclusão física do registro principal removerá automaticamente seus snapshots por meio da chave estrangeira com:

```text
ON DELETE CASCADE
```

Esse comportamento é deliberado porque o histórico definido nesta decisão acompanha o ciclo de vida do registro. A trilha de auditoria da exclusão pertence a outro mecanismo.

### Descoberta do modelo

O backend utilizará o modelo construído pelo Entity Framework Core como fonte de verdade para descobrir:

- Tipo CLR;
- Schema de origem;
- Tabela de origem;
- Chave primária;
- Nome físico das colunas;
- Tipo de armazenamento PostgreSQL;
- Nulabilidade;
- Propriedades geradas;
- Shadow properties.

O `EntityMetadata.TableName` será utilizado para correlacionar o metadado com a entidade mapeada.

Nesta primeira versão, o schema não será persistido no `EntityMetadata`. Caso mais de uma entidade seja encontrada com o mesmo nome de tabela em schemas diferentes, o engenho deverá interromper o provisionamento e retornar um diagnóstico explícito.

### Sincronização estrutural

A sincronização da tabela histórica será aditiva por padrão.

O engenho poderá:

- Criar uma tabela ausente;
- Criar uma coluna histórica ausente;
- Criar ou substituir a função da trigger;
- Criar uma trigger ausente;
- Criar índices e constraints ausentes.

O engenho não deverá remover automaticamente colunas históricas que deixaram de existir na entidade de origem.

Alterações incompatíveis de tipo deverão interromper o provisionamento e gerar um diagnóstico explícito. Conversões destrutivas ou ambíguas não serão executadas automaticamente.

### Segurança

Nomes de schemas, tabelas, colunas, índices, funções e triggers deverão ser obtidos exclusivamente do modelo do Entity Framework Core e de metadados previamente validados.

Identificadores SQL deverão ser validados segundo as convenções da plataforma e escapados com os mecanismos apropriados do Npgsql.

Valores deverão continuar parametrizados.

O engenho não deverá montar DDL ou consultas utilizando nomes arbitrários enviados pelo cliente.

A estratégia de permissões para execução de DDL em homologação e produção deverá respeitar o processo de implantação da Atlas. Sempre que possível, privilégios de migração e provisionamento deverão ser separados dos privilégios comuns de execução da aplicação.

## Justificativa

### Alternativa: tabela genérica com snapshots em `jsonb`

Foi rejeitada como modelo principal porque não forneceria chave estrangeira real para cada registro de origem nem exclusão automática com `ON DELETE CASCADE`.

Também reduziria a correspondência direta entre a estrutura histórica e a tabela principal.

### Alternativa: uma entidade C# histórica por entidade versionada

Foi rejeitada porque exigiria criar classes, configurações, `DbSet`s e migrations específicas para cada entidade, aumentando o custo de manutenção e reduzindo a capacidade de provisionamento automático.

### Alternativa: interceptor do Entity Framework Core como único mecanismo de captura

Foi rejeitada como mecanismo principal porque:

- Inclusões com IDs gerados pelo banco exigiriam coordenação após o `SaveChanges`;
- Seria necessário controlar uma segunda persistência e evitar recursão;
- Alterações realizadas fora do mesmo `DbContext` não seriam capturadas;
- A atomicidade seria mais complexa de garantir, especialmente em inclusões.

O interceptor continuará responsável pelos campos de auditoria da entidade principal.

### Alternativa: triggers mantidas manualmente no banco

Foi rejeitada porque produziria lógica estrutural difícil de rastrear, duplicada entre ambientes e sujeita a divergência quando novas entidades ou colunas fossem adicionadas.

As triggers serão executadas pelo PostgreSQL, mas sua definição e sincronização serão governadas pelo código C# versionado.

### Alternativa: remover a trigger ao desabilitar o histórico

Foi rejeitada nesta versão para manter a infraestrutura estável e evitar ciclos de remoção e recriação.

A trigger permanecerá instalada e consultará `EntityMetadata.EnableHistory` antes de gravar um snapshot.

## Consequências

### Ficou mais fácil

- Registrar snapshots de inclusões e atualizações com atomicidade;
- Preservar compatibilidade com o controle de concorrência por `xmin`;
- Excluir o histórico automaticamente com o registro principal;
- Consultar o histórico de qualquer entidade por um fluxo genérico;
- Habilitar e desabilitar a captura sem remover dados existentes;
- Criar infraestrutura histórica sem entidades C# específicas;
- Capturar alterações originadas por diferentes caminhos de escrita no banco;
- Evoluir a plataforma mantendo o backend como fonte de governança.

### Ficou mais difícil

- O backend precisará provisionar e sincronizar objetos PostgreSQL dinamicamente;
- Cada operação versionada fará uma consulta adicional ao `EntityMetadata` dentro da trigger;
- Alterações de schema precisarão considerar a tabela histórica e a função correspondente;
- A aplicação precisará tratar tipos incompatíveis sem alterações automáticas destrutivas;
- O ambiente de implantação precisará definir uma estratégia segura para permissões de DDL;
- O volume de armazenamento crescerá conforme a quantidade de snapshots;
- A consulta genérica precisará comparar versões consecutivas para apresentar valores anteriores e atuais;
- A ausência temporária do schema no `EntityMetadata` exige validação contra ambiguidades entre schemas.

## Observações de implementação

- O engenho pertence à plataforma e não poderá conhecer entidades específicas de clientes;
- Entidades da plataforma ou de negócio poderão ser versionadas quando possuírem `EntityMetadata` e `EnableHistory = true`;
- A primeira implementação deverá ignorar a shadow property vinculada ao `xmin`;
- Tabelas do próprio schema `history` não poderão ser versionadas;
- A tabela `platform.entity_metadata` não deverá receber histórico enquanto for utilizada pela própria trigger para decidir se a captura está habilitada;
- Campos sensíveis deverão ser avaliados antes de habilitar histórico para uma entidade;
- O provisionamento deverá ser idempotente;
- A consulta de histórico deverá ser paginada;
- O backend deverá resolver o nome do usuário por meio dos valores `CreatedBy` ou `LastModifiedBy` presentes no snapshot;
- A exibição de rótulos amigáveis de campos poderá evoluir futuramente para metadados próprios de propriedades;
- A funcionalidade deverá ser adicionada ao catálogo da plataforma quando estiver disponível para consumo.

## Referências

- ADR-001: Uso do EF Core na Camada de Aplicação;
- ADR-003: Segregação Arquitetural entre Plataforma e Negócio;
- ADR-004: Convenções de Idioma e Nomenclatura;
- ADR-005: Estratégia de Schemas no PostgreSQL;
- ADR-006: Contexto Único e Migrations Unificadas;
- ADR-007: Plataforma-base e Evolução dos Produtos de Cliente.

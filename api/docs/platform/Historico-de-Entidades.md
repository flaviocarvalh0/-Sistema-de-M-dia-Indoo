# Histórico de Entidades

## Objetivo

O histórico de entidades é uma capacidade transversal da plataforma Atlas responsável por registrar e disponibilizar versões sucessivas de registros persistidos.

A funcionalidade existe para permitir que usuários autenticados consultem como um registro evoluiu ao longo do tempo, identificando:

- quando cada versão foi capturada;
- qual usuário realizou a alteração;
- quais campos foram modificados;
- quais eram os valores anteriores e atuais;
- se a versão representa a criação inicial do registro.

Este histórico é operacional e não substitui uma trilha de auditoria imutável com finalidade jurídica, regulatória ou forense.

## Escopo

O mecanismo pode ser utilizado por entidades da plataforma e por entidades de qualquer módulo do sistema, desde que:

- a entidade esteja mapeada pelo EF Core;
- exista um registro correspondente em `platform.entity_metadata`;
- o histórico esteja habilitado no metadado;
- a entidade possua chave primária simples;
- a entidade não esteja mapeada como `owned entity`, `view` ou diretamente no schema reservado `history`;
- a entidade não seja `EntityMetadata` nesta versão.

## Visão geral da arquitetura

O mecanismo é dividido em responsabilidades independentes:

```text
EntityMetadata
    ↓
EntityHistoryModelResolver
    ↓
EntityHistoryDefinition
    ↓
PostgreSqlEntityHistorySchemaInspector
    ↓
EntityHistoryInfrastructureState
    ↓
EntityHistorySchemaComparer
    ↓
EntityHistoryProvisioningPlan
    ↓
PostgreSqlEntityHistoryProvisioningExecutor
```

Para leitura:

```text
routeKey
    ↓
EntityMetadata
    ↓
EntityHistoryModelResolver
    ↓
PostgreSqlEntityHistorySnapshotReader
    ↓
EntityHistorySnapshotComparer
    ↓
EntityHistoryQueryService
    ↓
DTOs da Application
    ↓
Endpoint HTTP
```

## Descoberta das entidades

A descoberta das entidades utiliza o modelo relacional do EF Core como fonte de verdade para:

- schema de origem;
- nome físico da tabela;
- chave primária;
- tipo relacional da chave;
- colunas persistidas;
- nomes físicos das colunas;
- tipos PostgreSQL;
- propriedades CLR correspondentes.

A resolução não depende de nomes informados pelo frontend e não aceita schema, tabela ou coluna arbitrários recebidos por requisição.

## Metadados

Cada entidade historizável deve possuir um registro em `platform.entity_metadata`.

Os principais campos utilizados pelo mecanismo são:

| Campo | Finalidade |
|---|---|
| `table_name` | Identifica a tabela física mapeada no modelo do EF Core. |
| `route_key` | Identificador público e estável utilizado pela API e pelo frontend. |
| `display_label` | Rótulo apresentado ao usuário. |
| `enable_history` | Define se novas versões devem ser capturadas. |

O atributo `MetadataConfigurationAttribute` define os valores iniciais no momento da criação do metadado. Depois que o registro existe, o banco de dados passa a ser a fonte de verdade para as configurações administrativas.

## Identificação pública por `routeKey`

A API utiliza `routeKey` em vez do nome físico da tabela:

```http
GET /api/platform/entity-history/{routeKey}/{recordId}
```

Exemplo:

```http
GET /api/platform/entity-history/system-parameter/42?pageSize=100
```

Essa decisão evita expor detalhes físicos do banco e desacopla o contrato HTTP de alterações como:

- renomeação de tabela;
- mudança de schema;
- reorganização de módulos;
- ajustes de convenção de persistência.

O `routeKey` deve ser tratado como identificador público da entidade. Portanto, precisa ser:

- obrigatório;
- único;
- estável;
- comparado de forma consistente;
- alterado somente por decisão consciente de compatibilidade.

Depois que uma rota de histórico for publicada, alterar o `routeKey` pode quebrar consumidores do frontend. Por isso, sua mudança deve ser tratada como alteração de contrato.

## Estrutura física do histórico

Cada entidade possui uma tabela histórica própria no schema `history`.

Exemplo para `platform.system_parameter`:

```text
history.platform_system_parameter_history
```

A tabela contém:

| Coluna | Descrição |
|---|---|
| `history_id` | Identificador técnico e sequencial da versão. |
| `record_id` | Chave do registro na tabela de origem. |
| `history_created_at` | Instante técnico em que o snapshot foi capturado. |
| demais colunas | Cópia integral das colunas persistidas da entidade, exceto a chave primária original e `xmin`. |

As colunas de snapshot são criadas como anuláveis para preservar compatibilidade com versões antigas quando novas propriedades forem adicionadas à entidade.

## Captura de versões

A captura é realizada por trigger PostgreSQL:

```text
AFTER INSERT OR UPDATE
```

A trigger executa uma função `PL/pgSQL` que grava um snapshot completo do registro após a persistência bem-sucedida.

Como a trigger é executada dentro da mesma transação da operação de origem:

- o snapshot só é persistido se a operação principal for confirmada;
- uma falha na captura impede o commit da alteração;
- gravações realizadas fora do EF Core também são capturadas;
- os campos de auditoria já preenchidos pelo interceptor do EF Core são copiados para o histórico.

## Habilitação e desabilitação

### Habilitação

Ao habilitar o histórico:

1. o metadado da entidade é carregado;
2. a definição relacional é resolvida;
3. o estado do PostgreSQL é inspecionado;
4. um plano de provisionamento é criado;
5. schema, tabela, colunas, índice, função, foreign key e trigger são reconciliados;
6. `enable_history` é definido como `true`;
7. a transação é confirmada.

A infraestrutura é provisionada antes da ativação do metadado para evitar uma janela em que o histórico esteja habilitado sem uma trigger funcional.

### Desabilitação

Ao desabilitar o histórico:

- `enable_history` passa para `false`;
- tabelas, funções, índices, foreign keys e triggers são preservados;
- os snapshots existentes continuam disponíveis;
- novas alterações deixam de gerar snapshots.

A função da trigger consulta `platform.entity_metadata` e retorna sem inserir quando o histórico está desabilitado.

## Exclusão do registro de origem

A tabela histórica possui uma foreign key para o registro de origem com:

```sql
ON DELETE CASCADE
```

Consequentemente, quando o registro original é excluído, todas as suas versões históricas também são removidas.

Essa decisão faz parte do caráter operacional da funcionalidade. O histórico não deve ser utilizado como trilha de auditoria imutável de registros excluídos.

## Sincronização estrutural

A sincronização é aditiva e conservadora.

O mecanismo pode:

- criar o schema histórico;
- criar a tabela histórica;
- adicionar colunas novas;
- criar ou atualizar a função;
- criar a trigger;
- criar o índice;
- criar a foreign key.

O mecanismo não executa automaticamente:

- remoção de coluna;
- alteração destrutiva de tipo;
- renomeação de coluna;
- exclusão de tabela histórica;
- conversão automática de dados incompatíveis.

Quando uma incompatibilidade é detectada, a inicialização falha explicitamente e exige migration ou intervenção técnica revisada.

Colunas antigas permanecem preservadas fisicamente, mesmo que tenham sido removidas da entidade atual.

## Inicialização automática

A reconciliação ocorre no startup da aplicação após:

1. aplicação das migrations;
2. inicialização dos metadados das entidades.

Ordem obrigatória:

```text
EntityMetadataInitializer
    ↓
EntityHistoryInitializer
```

Se uma entidade estiver com histórico habilitado e sua infraestrutura não puder ser reconciliada, a inicialização da aplicação deve falhar. A plataforma não deve continuar operando silenciosamente em estado inconsistente.

O provisionamento utiliza advisory lock transacional do PostgreSQL para reduzir conflitos entre múltiplas instâncias iniciadas simultaneamente.

## Leitura e paginação

A leitura dos snapshots é paginada por cursor usando `history_id`.

Parâmetros:

| Parâmetro | Descrição |
|---|---|
| `routeKey` | Identificador público da entidade. |
| `recordId` | Identificador do registro. |
| `beforeHistoryId` | Cursor opcional para buscar versões anteriores. |
| `pageSize` | Quantidade máxima de versões retornadas. |

A ordenação é decrescente:

```sql
ORDER BY history_id DESC
```

O leitor busca um item adicional para permitir que a última versão exibida na página seja comparada com sua predecessora.

## Comparação entre versões

Cada snapshot é comparado com a versão imediatamente anterior.

Exemplo:

```text
Versão 3: B → C
Versão 2: A → B
Versão 1: null → A
```

Na primeira versão conhecida:

- `isInitialVersion` é `true`;
- valores não nulos são apresentados como alteração de `null` para o valor inicial.

Campos de auditoria não são exibidos como alterações funcionais:

- `Created`;
- `CreatedBy`;
- `LastModified`;
- `LastModifiedBy`.

Esses campos são usados no cabeçalho da versão para informar data e usuário responsável.

## Resolução do usuário responsável

Os identificadores armazenados em `CreatedBy` e `LastModifiedBy` são resolvidos em lote pelo serviço de identidade já disponível na plataforma.

Isso evita uma consulta individual por versão e previne o problema de N+1 queries.

Quando o usuário não puder ser encontrado, o identificador original é utilizado como fallback.

## Endpoint HTTP

Endpoint atual:

```http
GET /api/platform/entity-history/{routeKey}/{recordId}
```

Exemplo com paginação:

```http
GET /api/platform/entity-history/system-parameter/42?pageSize=100&beforeHistoryId=250
```

O endpoint:

- exige usuário autenticado;
- não recebe schema;
- não recebe nome de tabela;
- não recebe nome de coluna;
- resolve a entidade pelo `routeKey` registrado no metadado;
- utiliza consultas parametrizadas para valores;
- utiliza identificadores provenientes exclusivamente do modelo EF Core.

Nesta versão, não existe uma permissão específica para leitura de histórico. O acesso é permitido a usuários autenticados, seguindo a decisão atual da plataforma.

Caso módulos futuros possuam restrições mais granulares de visualização, o endpoint deverá evoluir para aplicar a mesma autorização usada na leitura do registro de origem.

## Resposta da API

Exemplo simplificado:

```json
{
  "routeKey": "system-parameter",
  "entityLabel": "Parâmetros",
  "recordId": 42,
  "historyEnabled": true,
  "versions": [
    {
      "historyId": 103,
      "capturedAt": "2026-07-29T23:45:11.248Z",
      "changedAt": "2026-07-29T23:45:11.201Z",
      "changedById": "user-id",
      "changedByName": "Administrador",
      "isInitialVersion": false,
      "changes": [
        {
          "propertyName": "Value",
          "displayLabel": "Value",
          "previousValue": "initial",
          "currentValue": "updated",
          "previousDisplayValue": "initial",
          "currentDisplayValue": "updated"
        }
      ]
    }
  ],
  "hasMore": false,
  "nextCursor": null
}
```

## Segurança

As seguintes garantias devem ser preservadas:

- o frontend nunca informa identificadores físicos do banco;
- o `routeKey` precisa existir em `EntityMetadata`;
- a tabela e o schema são resolvidos pelo modelo do EF Core;
- identificadores PostgreSQL são escapados;
- valores são enviados por parâmetros;
- o endpoint exige autenticação;
- DDL não pode ser construído com dados arbitrários da requisição;
- falhas de compatibilidade estrutural interrompem o startup.

## Limitações da versão atual

- não existe baseline automático para registros já existentes ao habilitar o histórico;
- registros excluídos perdem seus snapshots por cascade;
- não há comparação entre duas versões arbitrárias escolhidas pelo usuário;
- não há metadados amigáveis por campo além de `[Display]` e nome CLR;
- não há permissão específica por entidade ou módulo;
- alterações destrutivas de schema exigem migration ou intervenção explícita;
- o metadado identifica a entidade por `table_name`, sem coluna de schema nesta versão;
- a API retorna coleção vazia tanto para registros sem versões quanto para IDs inexistentes.

## Operação e diagnóstico

Logs recomendados:

### Information

- início e fim da reconciliação no startup;
- habilitação ou desabilitação de histórico;
- falha geral de inicialização.

### Debug

- ações de provisionamento por entidade;
- quantidade de etapas executadas;
- detalhes técnicos do plano.

### Error

- incompatibilidade de schema;
- falha de DDL;
- falha na resolução do modelo;
- falha na inicialização de uma entidade habilitada.

Não é recomendado registrar o SQL completo em nível `Information`.

## Testes esperados

A funcionalidade deve manter cobertura para:

- descoberta de entidade pelo modelo EF Core;
- geração segura de SQL;
- limite de identificadores PostgreSQL;
- inspeção dos objetos no catálogo do PostgreSQL;
- sincronização aditiva;
- rollback em falha;
- criação de snapshot no `INSERT`;
- criação de snapshot no `UPDATE`;
- interrupção da captura após desabilitação;
- retomada após reabilitação;
- exclusão em cascata;
- inicialização idempotente;
- leitura paginada;
- baseline entre páginas;
- comparação entre versões;
- resolução em lote dos usuários;
- autenticação do endpoint;
- consulta ponta a ponta pela API.

## Referências

- `ADR-009: Estratégia de Versionamento Histórico de Entidades`;
- `docs/Platform/Funcionalidades-da-Plataforma.md`;
- configurações de `EntityMetadata`;
- contratos públicos em `Atlas.Application.Platform.EntityHistory`;
- implementação técnica em `Atlas.Infrastructure.Data.History`.

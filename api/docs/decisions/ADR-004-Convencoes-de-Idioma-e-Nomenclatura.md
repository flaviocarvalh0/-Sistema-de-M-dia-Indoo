# ADR-004: Convenções de Idioma e Nomenclatura

## Status

Aceito

## Data da decisão

2026-07-17

## Contexto

A Atlas será mantida por uma equipe brasileira e utilizada na construção de domínios de clientes cuja linguagem de negócio é predominantemente portuguesa.

Ao mesmo tempo, componentes técnicos e estruturais da plataforma seguem terminologia amplamente consolidada em inglês. A ausência de uma regra explícita pode produzir traduções artificiais, nomes mistos e inconsistências entre código, banco, migrations e documentação.

## Decisão

A nomenclatura seguirá a fronteira arquitetural definida no ADR-003.

### Plataforma

Classes, interfaces, propriedades, métodos, comandos, consultas e componentes pertencentes à plataforma serão nomeados em inglês.

Exemplos:

```text
SystemModule
SystemMenuItem
SystemParameter
EmailMessage
UserSession
PermissionService
CreateRoleCommand
GetSystemParametersQuery
```

### Negócio

Substantivos e comportamentos específicos do domínio do cliente poderão ser nomeados em português.

Exemplos:

```text
Empresa
Funcionario
Contrato
SaldoDiario
SincronizarContasCommand
CalcularScoreCommand
```

### Padrões técnicos

Termos técnicos consolidados permanecerão em inglês:

```text
Command
Query
Handler
Validator
Dto
Result
Middleware
Behaviour
Endpoint
Service
Provider
Worker
```

Operações CRUD poderão manter verbos em inglês para preservar uniformidade técnica:

```text
CreateEmpresaCommand
UpdateEmpresaCommand
DeleteEmpresaCommand
GetEmpresaByIdQuery
GetEmpresasQuery
```

Comportamentos próprios do domínio deverão preferir a linguagem natural do negócio:

```text
ConciliarSaldoCommand
GerarRelatorioMensalCommand
ReprocessarMovimentacoesCommand
```

Construções parcialmente traduzidas e artificiais deverão ser evitadas:

```text
SynchronizeContasCommand
GenerateRelatorioMensalCommand
CalculateSaldoDiarioCommand
```

### C#

Tipos e membros seguirão as convenções oficiais da linguagem:

- `PascalCase` para classes, records, enums, propriedades e métodos;
- `camelCase` para parâmetros e variáveis locais;
- prefixo `I` para interfaces;
- nomes claros e completos em vez de abreviações ambíguas.

### PostgreSQL

Schemas, tabelas, colunas, índices e constraints utilizarão letras minúsculas e `snake_case`.

Tabelas serão nomeadas no singular.

Chaves primárias utilizarão, por padrão:

```text
id
```

Chaves estrangeiras utilizarão:

```text
<entidade>_id
```

Campos booleanos deverão possuir nomes afirmativos:

```text
ativo
permite_envio
enviar_relatorio
```

Campos de auditoria, quando aplicáveis, seguirão:

```text
criado_em
criado_por
atualizado_em
atualizado_por
excluido_em
excluido_por
```

### Commits

Commits utilizarão tipo e escopo padronizados em inglês, com descrição em português.

Exemplos:

```text
feat(platform): adiciona gerenciamento de grupos
feat(business): adiciona cadastro de empresas
fix(platform): corrige validação de sessão
docs(architecture): registra estratégia de migrations
```

### Migrations

Migrations utilizarão prefixo arquitetural e descrição em português sem acentos ou caracteres especiais.

Exemplos:

```text
Platform_CriarEstruturaInicial
Platform_AdicionarHistoricoDeEmails
Business_AdicionarEmpresas
Shared_AdicionarCamposDeAuditoria
```

O prefixo `Shared` deverá ser reservado a alterações técnicas verdadeiramente transversais. Entidades de negócio compartilhadas continuam pertencendo ao prefixo `Business`.

## Justificativa

### Alternativa: nomear todo o código em inglês

Foi rejeitada para o domínio específico do cliente porque poderia afastar o código da linguagem utilizada pela equipe e pelos especialistas do negócio.

### Alternativa: nomear todo o código em português

Foi rejeitada para a plataforma e para padrões técnicos porque reduziria a aderência ao vocabulário do ecossistema .NET e produziria traduções pouco naturais.

### Alternativa: permitir escolha livre por desenvolvedor

Foi rejeitada porque produziria inconsistência, reduziria a previsibilidade e dificultaria revisão e pesquisa no código.

## Consequências

### Ficou mais fácil

- Reconhecer visualmente a fronteira entre plataforma e negócio;
- Manter a linguagem do domínio próxima da equipe e do cliente;
- Preservar vocabulário técnico familiar ao ecossistema;
- Padronizar commits, migrations, banco e código;
- Evitar traduções artificiais.

### Ficou mais difícil

- Alguns nomes combinarão verbo técnico em inglês e substantivo de negócio em português;
- A equipe deverá decidir quando um comportamento é CRUD técnico ou regra específica do domínio;
- Revisões deverão impedir misturas inconsistentes de idioma.

# Implantação de Migrations

## Objetivo

Este documento define o processo inicial para aplicar migrations em homologação e produção.

A implantação será feita por script SQL gerado das migrations versionadas, revisado e testado antes da execução.

A aplicação não aplicará migrations automaticamente no startup desses ambientes.

## Princípios

- A mesma sequência versionada será utilizada em todos os ambientes;
- O script será gerado a partir da versão exata da aplicação;
- O SQL será revisado antes da execução;
- O script será testado antes de produção;
- Backup e recuperação serão considerados;
- Origem e destino da migration serão conhecidos;
- Segredos não serão incorporados ao script;
- Banco e aplicação farão parte do mesmo plano de release.

## Responsabilidades

### Desenvolvimento

- Criar e revisar migrations;
- Testar localmente;
- Gerar SQL;
- Documentar riscos e requisitos.

### Revisão técnica

- Confirmar origem e destino;
- Avaliar operações destrutivas, locks, duração e volume;
- Confirmar compatibilidade entre banco e aplicação.

### Responsável pelo ambiente

- Confirmar backup, janela, banco e credencial;
- Executar o script aprovado;
- Preservar evidências;
- Validar o resultado e comunicar falhas.

## Identificar estado do ambiente

```sql
select *
from "__EFMigrationsHistory"
order by "MigrationId";
```

Confirmar a última migration, o banco correto, o ambiente correto e a versão da aplicação.

Não editar essa tabela manualmente no fluxo normal.

## Script incremental

Preferido quando o estado do ambiente é conhecido:

```bash
dotnet ef migrations script <MIGRATION_ATUAL> <MIGRATION_DESTINO> \
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj \
  --startup-project src/Atlas.Web/Atlas.Web.csproj \
  --context ApplicationDbContext \
  --output artifacts/migrations/<VERSAO>/Atualizacao.sql
```

## Script idempotente

```bash
dotnet ef migrations script --idempotent \
  --project src/Atlas.Infrastructure/Atlas.Infrastructure.csproj \
  --startup-project src/Atlas.Web/Atlas.Web.csproj \
  --context ApplicationDbContext \
  --output artifacts/migrations/<VERSAO>/Atualizacao-Idempotente.sql
```

O script idempotente aplica apenas migrations ausentes, mas não elimina a necessidade de conhecer o ambiente, revisar o SQL, testar e detectar drift.

## Pacote de implantação

```text
artifacts/
└── migrations/
    └── <VERSAO>/
        ├── Atualizacao.sql
        ├── README.md
        └── Validacao.sql
```

O `README.md` deve registrar:

```text
Aplicação:
Versão:
Migration de origem:
Migration de destino:
Commit:
Data de geração:
Responsável:
Ambientes:
Riscos:
Tempo observado em teste:
Requer indisponibilidade:
Requer backup:
Procedimento de retorno:
```

`Validacao.sql` deve conter apenas consultas não destrutivas.

Esses artefatos poderão ser produzidos pelo pipeline quando houver CI/CD.

## Revisão do script

Verificar:

- Origem e destino;
- Atualização do histórico;
- Schemas;
- Objetos removidos;
- Alterações de tipo e nulabilidade;
- Transformações de dados;
- Índices, chaves e constraints;
- Locks e duração;
- Operações não transacionais;
- SQL específico do PostgreSQL;
- Idempotência, quando aplicável.

Nenhum script será aprovado apenas por ter sido gerado pelo EF Core.

## Testes obrigatórios

1. Preparar banco equivalente ou cópia representativa;
2. Confirmar migration inicial;
3. Aplicar script;
4. Registrar duração;
5. Validar objetos;
6. Executar nova versão da aplicação;
7. Executar testes funcionais;
8. Avaliar logs;
9. Testar recuperação quando viável.

Alterações de grande volume exigem volume representativo.

## Ordem de implantação

### Schema compatível com a aplicação atual

```text
Backup
→ Script
→ Validação
→ Publicação da aplicação
→ Testes
```

### Mudança dependente do código novo

```text
Backup
→ Controle de tráfego ou parada
→ Script
→ Publicação
→ Validação
→ Retorno do tráfego
```

### Mudança em etapas

```text
Release 1: adicionar estrutura compatível
Release 2: migrar uso e dados
Release 3: remover estrutura antiga
```

## Backup e recuperação

Antes de mudanças destrutivas ou relevantes:

- Confirmar backup recente;
- Confirmar capacidade de restauração;
- Registrar horário e responsável;
- Conhecer o tempo esperado de recuperação.

Backup nunca testado não deve ser tratado como estratégia comprovada.

## Rollback

Planejar antes da aplicação:

- Rollback da aplicação mantendo schema compatível;
- Script reverso revisado;
- Restauração de backup;
- Roll-forward com nova correção.

Não executar automaticamente o `Down` em produção sem revisar o SQL e o impacto em dados.

## Falha na execução

1. Interromper novas tentativas;
2. Preservar erro e ponto da falha;
3. Verificar transação;
4. Verificar histórico;
5. Verificar objetos parciais;
6. Não forçar sucesso editando o histórico;
7. Avaliar restauração, correção ou roll-forward;
8. Testar fora de produção;
9. Registrar a ocorrência;
10. Retomar somente após decisão técnica.

## Alterações emergenciais

Só serão aceitáveis quando houver risco operacional imediato, autorização, SQL preservado e reconciliação posterior no modelo e nas migrations.

A urgência não elimina a necessidade de rastreabilidade.

## Permissões

Preferencialmente:

```text
Usuário da aplicação → operações normais de dados
Usuário de implantação → permissões controladas de DDL
```

A aplicação em runtime não precisa ter permissão permanente para alterar schema.

## Startup e automação

Não utilizar migrations automáticas no startup de homologação ou produção.

Migration bundles poderão ser avaliados futuramente quando houver pipeline, artefatos controlados, execução única, conexão segura e observabilidade. Essa adoção deverá ser documentada antes de substituir scripts SQL revisados.

## Checklist pré-implantação

- [ ] Release e commit identificados;
- [ ] Origem e destino confirmados;
- [ ] Script da versão correta;
- [ ] Script revisado e testado;
- [ ] Duração avaliada;
- [ ] Riscos documentados;
- [ ] Operações destrutivas aprovadas;
- [ ] Backup e recuperação confirmados;
- [ ] Janela e responsável definidos;
- [ ] Ordem banco/aplicação definida;
- [ ] Validação e retorno preparados.

## Checklist pós-implantação

- [ ] Script concluído;
- [ ] Migration registrada;
- [ ] Objetos validados;
- [ ] Aplicação publicada;
- [ ] Health checks e testes aprovados;
- [ ] Logs verificados;
- [ ] Duração e evidências registradas;
- [ ] Incidentes documentados;
- [ ] Ambiente liberado.

## Referências internas

- `docs/database/Gerenciamento-de-Migrations.md`
- `docs/development/Guia-Rapido-de-Migrations.md`
- `docs/decisions/ADR-006-Contexto-Unico-e-Migrations-Unificadas.md`

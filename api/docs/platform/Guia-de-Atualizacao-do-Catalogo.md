# Guia de Atualização do Catálogo de Funcionalidades

## Objetivo

Este documento define como manter atualizado o arquivo:

```text
docs/platform/Funcionalidades-da-Plataforma.md
```

O catálogo deve representar o estado funcional conhecido da plataforma Atlas.

## Quando atualizar

O catálogo deverá ser revisado quando:

- Uma funcionalidade for adicionada;
- Um endpoint novo representar capacidade funcional relevante;
- Uma funcionalidade ganhar ou perder operações;
- Uma permissão for adicionada ou alterada;
- Uma funcionalidade mudar de status;
- Uma capacidade for removida;
- Uma funcionalidade for transferida entre `Platform` e `Business`;
- Uma configuração obrigatória for introduzida;
- Uma capacidade for extraída para pacote;
- Uma funcionalidade planejada for iniciada ou concluída.

Correções internas sem mudança funcional não exigem necessariamente alteração do catálogo.

## Responsabilidade

O autor da mudança funcional é responsável pela atualização inicial.

O revisor deverá confirmar que:

- O catálogo foi atualizado;
- O status está correto;
- A descrição não promete comportamento inexistente;
- As permissões estão corretas;
- A fronteira arquitetural foi respeitada;
- Os caminhos indicados existem;
- A documentação não expõe segredos.

## Estrutura de uma funcionalidade

Utilize o seguinte modelo:

```md
## Nome da funcionalidade

**Status:** Implementado

### Finalidade

Explique o problema resolvido e o propósito da capacidade.

### Capacidades

- Capacidade 1;
- Capacidade 2;
- Capacidade 3.

### Componente principal

```text
caminho/do/arquivo
```

### Permissões relacionadas

```text
Permission.Key
```

### Regras funcionais conhecidas

- Regra relevante;
- Limite importante.

### Observações

Inclua dependências, riscos ou limites arquiteturais.
```

Nem todas as subseções são obrigatórias. Mantenha apenas as que agregam informação real.

## Como escolher o status

### Implementado

Use quando o fluxo existe, as regras essenciais estão implementadas e a capacidade pode ser consumida conforme descrita.

### Parcial

Use quando o fluxo principal existe, mas ainda faltam operações ou comportamentos relevantes.

### Em validação

Use quando a implementação existe, mas ainda depende de testes, baseline, implantação ou estabilização.

### Planejado

Use quando a capacidade faz parte da direção conhecida, mas ainda não existe fluxo consumível.

### Descontinuado

Use quando o recurso não deve mais ser utilizado ou está sendo substituído.

## O que documentar

Priorize linguagem funcional:

```text
Permite consultar usuários e redefinir senhas.
```

Evite descrições apenas estruturais:

```text
Existe uma classe Users.cs com métodos estáticos.
```

Registre:

- Finalidade;
- Operações disponíveis;
- Regras relevantes;
- Permissões;
- Dependências;
- Limites;
- Estado atual.

## O que não documentar neste catálogo

Não transforme o catálogo em:

- Referência completa de API;
- Diagrama de classes;
- Relação de todos os arquivos;
- Manual de implantação;
- Procedimento de banco;
- ADR;
- Changelog;
- Backlog;
- Documentação específica de cliente.

Use links para documentos especializados.

## Atualização do resumo

Toda funcionalidade nova deve ser adicionada à tabela de resumo.

Quando o status mudar, atualize a tabela e a seção detalhada.

## Permissões

Documente apenas chaves de permissão efetivamente utilizadas.

Não documente senhas, tokens, valores de configuração ou qualquer segredo.

## Funcionalidades específicas de clientes

Funcionalidades de negócio não devem ser adicionadas ao catálogo da plataforma-base.

Quando um produto necessitar de catálogo próprio, utilize, por exemplo:

```text
docs/business/Funcionalidades-do-Produto.md
```

## Pull Request

### Checklist do autor

- [ ] O resumo foi atualizado;
- [ ] A seção funcional foi atualizada;
- [ ] O status está correto;
- [ ] As permissões foram registradas;
- [ ] Os caminhos estão corretos;
- [ ] A descrição é funcional;
- [ ] Não há comportamento prometido que ainda não existe;
- [ ] Os limites `Platform` e `Business` foram respeitados;
- [ ] Links internos foram revisados.

### Checklist do revisor

- [ ] A funcionalidade existe conforme descrita;
- [ ] O status é defensável;
- [ ] Não há duplicidade;
- [ ] A documentação não virou uma cópia do código;
- [ ] As permissões estão corretas;
- [ ] Não há exposição de informação sensível;
- [ ] A mudança está coerente com os ADRs.

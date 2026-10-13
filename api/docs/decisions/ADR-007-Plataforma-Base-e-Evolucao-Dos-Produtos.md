# ADR-007: Plataforma-base e Evolução dos Produtos de Cliente

## Status

Aceito

## Data da decisão

2026-07-17

## Contexto

A Atlas deverá permitir que novas aplicações iniciem com recursos estruturais prontos, como segurança, autenticação, permissões, parâmetros, comunicação, menus e observabilidade.

Cada produto cliente evoluirá em ritmo próprio. Correções e novas funcionalidades da plataforma não podem depender do negócio de um cliente, nem ser propagadas indefinidamente por cópia manual sem identificação de versão.

No estágio inicial, a plataforma e o primeiro produto ainda podem compartilhar uma estrutura muito próxima. É necessário definir a direção de evolução sem introduzir imediatamente toda a infraestrutura de empacotamento.

## Decisão

Será mantida uma plataforma-base livre de entidades e regras específicas de clientes.

Novos produtos serão compostos por:

```text
Versão identificada da plataforma-base
+
Módulos de negócio do cliente
=
Produto final
```

### Conteúdo da plataforma-base

A plataforma-base poderá conter:

- Autenticação e autorização;
- Usuários, grupos, papéis e permissões;
- Sessões;
- Menus e módulos;
- Parâmetros;
- Comunicação e histórico de e-mails;
- Metadados;
- Ícones;
- Observabilidade;
- Sanitização de logs;
- Comportamentos de aplicação e infraestrutura reutilizável.

Ela não deverá conter entidades demonstrativas ou específicas, como `Empresa`, apenas para exemplificar a pasta `Business`.

### Identificação da origem

Cada produto deverá registrar a versão ou tag da plataforma que lhe deu origem.

Exemplo:

```text
Atlas Platform: v0.1.0-alpha.1
```

### Evolução da plataforma

Correções e funcionalidades reutilizáveis serão implementadas primeiramente no repositório oficial da plataforma.

Durante a fase inicial, a incorporação dessas alterações em produtos poderá exigir processo controlado de merge ou aplicação de commits identificados.

A cópia manual permanente de arquivos não será considerada estratégia final.

### Destino arquitetural

A evolução deverá conduzir progressivamente a:

1. Projetos da plataforma com fronteiras públicas claras;
2. Pacotes NuGet privados versionados;
3. Produtos consumidores com versões exatas das dependências;
4. Atualizações deliberadas e testadas por produto;
5. Aplicação sample sem regras de cliente;
6. Processo automatizado de build, testes e publicação.

### Versionamento

A plataforma e cada produto possuirão versões independentes.

Exemplo:

```text
Atlas Platform 1.3.0
Produto Cliente A 2.1.0
```

Uma atualização do produto não implica necessariamente uma nova versão da plataforma, e vice-versa.

Enquanto a API pública ainda estiver em estabilização, serão utilizadas versões preliminares:

```text
0.1.0-alpha.1
0.5.0-beta.1
0.9.0-rc.1
1.0.0
```

## Justificativa

### Alternativa: manter uma cópia independente da plataforma em cada cliente

Foi rejeitada como estratégia permanente porque gera deriva, dificulta correções de segurança e exige propagação manual para todos os produtos.

### Alternativa: desenvolver funcionalidades da plataforma diretamente em um cliente

Foi rejeitada como fluxo principal porque a solução específica pode introduzir dependências e pressupostos que não pertencem à plataforma reutilizável.

Uma correção emergencial poderá ser preparada em um produto, mas deverá ser generalizada e incorporada ao repositório oficial antes de ser distribuída.

### Alternativa: implantar pacotes NuGet antes do primeiro baseline

Foi adiada porque a plataforma precisa primeiro estabelecer Git, migrations, contratos públicos, documentação e processo de revisão.

## Consequências

### Ficou mais fácil

- Iniciar produtos com uma base funcional e consistente;
- Evoluir a plataforma independentemente de um cliente;
- Identificar a versão utilizada por cada aplicação;
- Planejar transição gradual para pacotes;
- Evitar que regras do primeiro cliente sejam tratadas como universais;
- Atualizar clientes de forma deliberada.

### Ficou mais difícil

- Durante a fase transitória, atualizações exigirão disciplina e rastreabilidade;
- Mudanças públicas precisarão considerar compatibilidade;
- A equipe deverá manter documentação de versões e origem;
- A extração futura para pacotes exigirá redução e estabilização da superfície pública.

## Etapas de adoção

1. Remover entidades demonstrativas de negócio da plataforma-base;
2. Inicializar o repositório Git e criar o baseline;
3. Criar a migration `Platform_CriarEstruturaInicial`;
4. Criar a primeira versão preliminar da plataforma;
5. Criar o produto cliente a partir dessa versão;
6. Registrar no produto a origem da plataforma;
7. Adicionar entidades do cliente com migrations `Business_*`;
8. Evoluir gradualmente para dependências empacotadas.

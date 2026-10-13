# Funcionalidades da Plataforma Atlas

A Atlas oferece recursos compartilhados para autenticação, usuários,
permissões, navegação, configurações e comunicação.

## Funcionalidades disponíveis

| Área | Funcionalidade | Descrição | Status |
|---|---|---|---|
| Segurança | Autenticação | Login, logout, sessão e refresh token | Disponível |
| Usuários | Gestão de usuários | Cadastro, edição, ativação e redefinição de senha | Disponível |
| Autorização | Grupos e permissões | Controle de acesso por grupo e por usuário | Disponível |
| Estrutura | Módulos | Organização das funcionalidades da aplicação | Disponível |
| Navegação | Menus dinâmicos | Menus hierárquicos conforme as permissões | Disponível |
| Configuração | Parâmetros globais | Configurações compartilhadas da aplicação | Disponível |
| Configuração | Metadados de entidades | Personalização do comportamento das entidades | Disponível |
| Interface | Ícones | Catálogo central de ícones | Disponível |
| Comunicação | E-mails | SMTP, histórico, teste e reprocessamento | Disponível |

## Em evolução

| Funcionalidade | Status |
|---|---|
| Migrations da plataforma-base | Em validação |
| Distribuição por pacotes NuGet privados | Planejado |
| Histórico de entidades | Em validação | Versionamento operacional por snapshots completos, com captura transacional por triggers PostgreSQL, provisionamento automático e consulta paginada por entidade. |

## Documentação técnica

- Documentação da API
- Arquitetura e ADRs
- Permissões
- Migrations
- Implantação
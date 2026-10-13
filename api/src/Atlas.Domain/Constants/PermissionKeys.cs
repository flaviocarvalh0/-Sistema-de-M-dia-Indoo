namespace Atlas.Domain.Constants;

/// <summary>
/// Centraliza todas as permissões (chaves de segurança) do Atlas Framework.
/// </summary>
public static class PermissionKeys
{
    /// <summary>
    /// Permissões relativas ao gerenciamento de usuários.
    /// </summary>
    public static class Users
    {
        /// <summary> Permite visualizar a listagem e os detalhes dos perfis de usuários. </summary>
        public const string View = "Users.View";

        /// <summary> Permite cadastrar novos usuários na plataforma. </summary>
        public const string Create = "Users.Create";

        /// <summary> Permite atualizar os dados cadastrais de usuários existentes. </summary>
        public const string Update = "Users.Update";

        /// <summary> Permite ativar usuários inativos, restabelecendo o acesso ao sistema. </summary>
        public const string Activate = "Users.Activate";

        /// <summary> Permite desativar usuários, bloqueando temporariamente o acesso à plataforma. </summary>
        public const string Deactivate = "Users.Deactivate";

        /// <summary> Permite forçar a redefinição de senha ou enviar link de reset para usuários. </summary>
        public const string ResetPassword = "Users.ResetPassword";

        /// <summary> Permite vincular um perfil de acesso (Role) a um usuário logado. </summary>
        public const string AssignRole = "Users.AssignRole";

        /// <summary> Permite remover um perfil de acesso (Role) previamente vinculado a um usuário. </summary>
        public const string RemoveRole = "Users.RemoveRole";
    }

    /// <summary>
    /// Permissões relativas ao controle de perfis de acesso (Roles) e suas matrizes de segurança.
    /// </summary>
    public static class Roles
    {
        /// <summary> Permite visualizar os perfis de acesso cadastrados no sistema. </summary>
        public const string View = "Roles.View";

        /// <summary> Permite criar novos perfis de acesso (ex: Operador, Financeiro). </summary>
        public const string Create = "Roles.Create";

        /// <summary> Permite alterar o nome ou escopo de um perfil de acesso existente. </summary>
        public const string Update = "Roles.Update";

        /// <summary> Permite excluir logicamente ou fisicamente um perfil de acesso. </summary>
        public const string Delete = "Roles.Delete";

        /// <summary> Permite gerenciar a matriz granular de permissões vinculadas a cada perfil. </summary>
        public const string ManagePermissions = "Roles.ManagePermissions";
    }

    /// <summary>
    /// Permissões relativas às configurações globais e serviços core do ecossistema.
    /// </summary>
    public static class Settings
    {
        /// <summary> Permite ler os parâmetros técnicos de configuração do servidor SMTP. </summary>
        public const string ViewSmtp = "Settings.ViewSmtp";

        /// <summary> Permite alterar as credenciais SMTP e disparar e-mails de teste em runtime. </summary>
        public const string ManageSmtp = "Settings.ManageSmtp";
    }

    /// <summary>
    /// Permissões relativas ao motor de parâmetros dinâmicos globais do sistema.
    /// </summary>
    public static class Parameters
    {
        /// <summary> Permite listar e consultar os parâmetros de sistema no painel administrativo. </summary>
        public const string View = "Parameters.View";

        /// <summary> Permite criar novos parâmetros dinâmicos globais para configuração do comportamento do sistema. </summary>
        public const string Create = "Parameters.Create";

        /// <summary> Permite alterar os valores dos parâmetros dinâmicos existentes, impactando o comportamento do sistema. </summary>
        public const string Update = "Parameters.Update";

        /// <summary> Permite excluir parâmetros dinâmicos globais cadastrados no sistema. </summary>
        public const string Delete = "Parameters.Delete";
    }

    /// <summary>
    /// Permissões relativas ao motor de mensageria assíncrona e controle do Outbox de e-mails.
    /// </summary>
    public static class EmailMessages
    {
        /// <summary> Permite listar, filtrar e auditar o histórico de mensagens de e-mail geradas pela plataforma. </summary>
        public const string View = "EmailMessages.View";

        /// <summary> Permite forçar o reenvio manual de mensagens de e-mail que falharam no fluxo de processamento. </summary>
        public const string Retry = "EmailMessages.Retry";
    }

    /// <summary>
    /// Permissões relativas ao gerenciamento do repositório central de ícones.
    /// </summary>
    public static class SystemIcons
    {
        /// <summary> Permite visualizar o catálogo de ícones do sistema. </summary>
        public const string View = "SystemIcons.View";

        /// <summary> Permite fazer o upload e cadastrar novos ícones na plataforma. </summary>
        public const string Create = "SystemIcons.Create";

        /// <summary> Permite atualizar os metadados ou substituir o arquivo de um ícone existente. </summary>
        public const string Update = "SystemIcons.Update";

        /// <summary> Permite excluir um ícone do repositório central. </summary>
        public const string Delete = "SystemIcons.Delete";
    }

    /// <summary>
    /// Permissões relativas à governança e customização dos metadados das entidades (tabelas) descobertas pela plataforma.
    /// </summary>
    public static class EntityMetadata
    {
        /// <summary> Permite visualizar a listagem e os detalhes das configurações de metadados das entidades. </summary>
        public const string View = "EntityMetadata.View";

        /// <summary> Permite atualizar rótulos, tamanhos de página, regras de exportação e comportamentos dinâmicos das entidades. </summary>
        public const string Update = "EntityMetadata.Update";
    }

    /// <summary>
    /// Permissões relativas ao ciclo de vida e ordenação dos módulos do sistema.
    /// </summary>
    public static class SystemModules
    {
        /// <summary> Permite listar e consultar os módulos ativos na plataforma. </summary>
        public const string View = "SystemModules.View";

        /// <summary> Permite criar e instanciar novos módulos com identidades visuais próprias. </summary>
        public const string Create = "SystemModules.Create";

        /// <summary> Permite alterar dados cadastrais, regras de ordenação e realizar a associação de metadados ao módulo. </summary>
        public const string Update = "SystemModules.Update";

        /// <summary> Permite remover permanentemente módulos da plataforma. </summary>
        public const string Delete = "SystemModules.Delete";
    }

    /// <summary>
    /// Permissões relativas à árvore de navegação e menus do sistema.
    /// </summary>
    public static class SystemMenus
    {
        /// <summary>
        /// Permite consultar árvores de menu configuradas na plataforma.
        /// </summary>
        public const string View = "SystemMenus.View";

        /// <summary>
        /// Permite criar itens na árvore de navegação dinâmica da plataforma.
        /// </summary>
        public const string Create = "SystemMenus.Create";

        /// <summary>
        /// Permite alterar itens de menu, incluindo rótulo, ordenação, ícone, pai e entidade associada.
        /// </summary>
        public const string Update = "SystemMenus.Update";

        /// <summary>
        /// Permite remover itens da árvore de navegação da plataforma.
        /// </summary>
        public const string Delete = "SystemMenus.Delete";
    }

    /// <summary>
    /// Permissões relativas ao gerenciamento de empresas.
    /// </summary>
    public static class Empresas
    {
        /// <summary>
        /// Permite visualizar a listagem e os detalhes das empresas cadastradas.
        /// </summary>
        public const string View = "Empresas.View";

        /// <summary>
        /// Permite cadastrar novas empresas.
        /// </summary>
        public const string Create = "Empresas.Create";

        /// <summary>
        /// Permite atualizar os dados de empresas existentes.
        /// </summary>
        public const string Update = "Empresas.Update";

        /// <summary>
        /// Permite remover empresas cadastradas.
        /// </summary>
        public const string Delete = "Empresas.Delete";
    }

    /// <summary>
    /// Retorna uma lista contendo absolutamente todas as permissões declaradas no framework.
    /// Útil para seeds de banco de dados e sincronização de perfis.
    /// </summary>
    public static IEnumerable<string> All()
    {
        return
        [
            Users.View,
            Users.Create,
            Users.Update,
            Users.Activate,
            Users.Deactivate,
            Users.ResetPassword,
            Users.AssignRole,
            Users.RemoveRole,

            Roles.View,
            Roles.Create,
            Roles.Update,
            Roles.Delete,
            Roles.ManagePermissions,

            Settings.ViewSmtp,
            Settings.ManageSmtp,

            Parameters.View,
            Parameters.Create,
            Parameters.Update,
            Parameters.Delete,

            EmailMessages.View,
            EmailMessages.Retry,

            SystemIcons.View,
            SystemIcons.Create,
            SystemIcons.Update,
            SystemIcons.Delete,

            EntityMetadata.View,
            EntityMetadata.Update,

            SystemModules.View,
            SystemModules.Create,
            SystemModules.Update,
            SystemModules.Delete,

            SystemMenus.View,
            SystemMenus.Create,
            SystemMenus.Update,
            SystemMenus.Delete,

            Empresas.View,
            Empresas.Create,
            Empresas.Update,
            Empresas.Delete,
        ];
    }
}
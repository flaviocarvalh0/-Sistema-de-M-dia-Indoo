namespace Atlas.Domain.Business.Constants;

/// <summary>
/// Permissões das entidades de negócio da Mídia Indoor, usadas em
/// <c>[Authorize(Policy = ...)]</c> nos commands e queries de Business.
/// </summary>
/// <remarks>
/// O valor de cada chave precisa ser idêntico à claim que o Atlas Studio grava
/// ao conceder a permissão: <c>ToPascalCasePlural(&lt;tabela&gt;)</c> +
/// <c>.View/.Create/.Update/.Delete</c> (ver <c>PermissionService</c>). Uma chave
/// escrita diferente compila, registra a política e nega acesso a todo mundo.
/// Novas entidades entram aqui na etapa em que forem criadas.
/// </remarks>
public static class BusinessPermissionKeys
{
    /// <summary>Tabela <c>business.parceiro</c> (gestão da operadora).</summary>
    public static class Parceiros
    {
        public const string View = "Parceiros.View";
        public const string Create = "Parceiros.Create";
        public const string Update = "Parceiros.Update";
        public const string Delete = "Parceiros.Delete";
    }

    /// <summary>Tabela <c>indoor.tela</c> (pareamento incluso).</summary>
    public static class Telas
    {
        public const string View = "Telas.View";
        public const string Create = "Telas.Create";
        public const string Update = "Telas.Update";
        public const string Delete = "Telas.Delete";
    }

    /// <summary>Tabela <c>indoor.midia</c> (envio ao storage incluso).</summary>
    public static class Midias
    {
        public const string View = "Midias.View";
        public const string Create = "Midias.Create";
        public const string Update = "Midias.Update";
        public const string Delete = "Midias.Delete";
    }

    /// <summary>Tabela <c>indoor.playlist</c> (itens vão junto).</summary>
    public static class Playlists
    {
        public const string View = "Playlists.View";
        public const string Create = "Playlists.Create";
        public const string Update = "Playlists.Update";
        public const string Delete = "Playlists.Delete";
    }

    public static IReadOnlyList<string> All() =>
    [
        Parceiros.View, Parceiros.Create, Parceiros.Update, Parceiros.Delete,
        Telas.View, Telas.Create, Telas.Update, Telas.Delete,
        Midias.View, Midias.Create, Midias.Update, Midias.Delete,
        Playlists.View, Playlists.Create, Playlists.Update, Playlists.Delete,
    ];
}

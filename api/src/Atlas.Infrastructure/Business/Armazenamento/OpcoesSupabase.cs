using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// Seção <c>Indoor:Armazenamento:Supabase</c>. A <see cref="ChaveServico"/> é
/// segredo (ADR-008): User Secrets no desenvolvimento, variável de ambiente
/// <c>Indoor__Armazenamento__Supabase__ChaveServico</c> nos servidores.
/// </summary>
internal sealed record OpcoesSupabase
{
    public const string Secao = "Indoor:Armazenamento:Supabase";

    /// <summary>Endereço do projeto (<c>https://&lt;projeto&gt;.supabase.co</c>).</summary>
    public required Uri Url { get; init; }

    /// <summary>Chave <c>service_role</c>: ignora as políticas do bucket. Nunca vai ao navegador.</summary>
    public required string ChaveServico { get; init; }

    /// <summary>Bucket privado das mídias.</summary>
    public string Bucket { get; init; } = "midias";

    /// <summary>Cria o bucket na subida da API se ele não existir.</summary>
    public bool CriarBucket { get; init; } = true;

    /// <summary>Base da Storage API (<c>{Url}/storage/v1/</c>).</summary>
    public Uri BaseStorage => new(Url, "storage/v1/");

    /// <summary>Lê a seção; <c>null</c> se a URL ou a chave não estão configuradas.</summary>
    public static OpcoesSupabase? Ler(IConfiguration configuration)
    {
        var secao = configuration.GetSection(Secao);
        var url = secao["Url"];
        var chave = secao["ChaveServico"];

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        if (!Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var endereco) || endereco.Scheme != Uri.UriSchemeHttps && !endereco.IsLoopback)
        {
            throw new InvalidOperationException($"'{Secao}:Url' deve ser um endereço https absoluto.");
        }

        return new OpcoesSupabase
        {
            Url = endereco,
            ChaveServico = chave,
            Bucket = string.IsNullOrWhiteSpace(secao["Bucket"]) ? "midias" : secao["Bucket"]!,
            CriarBucket = !bool.TryParse(secao["CriarBucket"], out var criar) || criar,
        };
    }
}

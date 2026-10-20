using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// Seção <c>Indoor:Armazenamento:R2</c> (Cloudflare R2, API S3, ADR-025). O
/// <see cref="SegredoAcesso"/> é segredo (ADR-008): User Secrets no
/// desenvolvimento, <c>Indoor__Armazenamento__R2__SegredoAcesso</c> nos servidores.
/// </summary>
internal sealed record OpcoesR2
{
    public const string Secao = "Indoor:Armazenamento:R2";

    /// <summary>Account ID da Cloudflare (o endereço vira <c>https://{conta}.r2.cloudflarestorage.com</c>).</summary>
    public required string Conta { get; init; }

    /// <summary>Access Key ID do token de API do R2 (permissão "Object Read &amp; Write" só neste bucket).</summary>
    public required string ChaveAcesso { get; init; }

    /// <summary>Secret Access Key do mesmo token.</summary>
    public required string SegredoAcesso { get; init; }

    /// <summary>Bucket privado das mídias.</summary>
    public string Bucket { get; init; } = "midias";

    /// <summary>
    /// Endereço da API S3. Padrão: <c>https://{Conta}.r2.cloudflarestorage.com</c>.
    /// Só muda em teste (um S3 local) ou na jurisdição europeia (<c>.eu.r2...</c>).
    /// </summary>
    public required Uri Endereco { get; init; }

    /// <summary>Endereço de um objeto: <c>{Endereco}/{Bucket}/{chave}</c> (estilo caminho).</summary>
    public Uri Objeto(string chave) =>
        new($"{Endereco.AbsoluteUri.TrimEnd('/')}/{AssinaturaS3.Codificar(Bucket)}/{AssinaturaS3.Codificar(chave, manterBarra: true)}");

    public Uri BucketUri(string consulta = "") =>
        new($"{Endereco.AbsoluteUri.TrimEnd('/')}/{AssinaturaS3.Codificar(Bucket)}{(consulta.Length > 0 ? "?" + consulta : string.Empty)}");

    /// <summary>Lê a seção; <c>null</c> se conta, chave ou segredo não estão configurados.</summary>
    public static OpcoesR2? Ler(IConfiguration configuration)
    {
        var secao = configuration.GetSection(Secao);
        var conta = secao["Conta"];
        var chave = secao["ChaveAcesso"];
        var segredo = secao["SegredoAcesso"];

        if (string.IsNullOrWhiteSpace(conta) || string.IsNullOrWhiteSpace(chave) || string.IsNullOrWhiteSpace(segredo))
        {
            return null;
        }

        var enderecoTexto = string.IsNullOrWhiteSpace(secao["Endereco"])
            ? $"https://{conta.Trim()}.r2.cloudflarestorage.com"
            : secao["Endereco"]!;

        if (!Uri.TryCreate(enderecoTexto.TrimEnd('/'), UriKind.Absolute, out var endereco)
            || (endereco.Scheme != Uri.UriSchemeHttps && !endereco.IsLoopback))
        {
            throw new InvalidOperationException($"'{Secao}:Endereco' deve ser um endereço https absoluto.");
        }

        return new OpcoesR2
        {
            Conta = conta.Trim(),
            ChaveAcesso = chave.Trim(),
            SegredoAcesso = segredo.Trim(),
            Bucket = string.IsNullOrWhiteSpace(secao["Bucket"]) ? "midias" : secao["Bucket"]!.Trim(),
            Endereco = endereco,
        };
    }
}

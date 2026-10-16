using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>Para quais TVs uma versão do app vai (ADR-021).</summary>
public enum DistribuicaoVersao
{
    /// <summary>Enviada, mas nenhuma TV recebe (ou recolhida depois de um problema).</summary>
    Suspensa = 1,

    /// <summary>Só as telas marcadas como "TV de teste".</summary>
    Teste = 2,

    /// <summary>Todas as TVs.</summary>
    Todas = 3,
}

/// <summary>
/// Uma versão do app das TVs (APK) enviada pela operadora. A TV que roda uma
/// versão mais antiga recebe na sincronização o endereço desta, baixa,
/// confere o SHA-256 e instala sozinha (ADR-021).
/// </summary>
[MetadataConfiguration("Versões do app das TVs")]
public class VersaoApp : BaseAuditableEntity
{
    /// <summary>Limite por arquivo do plano gratuito do Supabase Storage.</summary>
    public const long TamanhoMaximoBytes = 50L * 1024 * 1024;

    /// <summary>
    /// O <c>versionCode</c> do Android (o número depois do <c>+</c> no
    /// <c>version</c> do pubspec.yaml). Único; maior = mais nova.
    /// </summary>
    public int Codigo { get; set; }

    /// <summary>O <c>versionName</c> (ex.: <c>1.3.0</c>), só para exibir.</summary>
    public string Nome { get; set; } = null!;

    /// <summary>O que mudou (só no painel).</summary>
    public string? Notas { get; set; }

    /// <summary>Objeto no bucket: <c>app/{guid}.apk</c>.</summary>
    public string ChaveArmazenamento { get; set; } = null!;

    public long TamanhoBytes { get; set; }

    /// <summary>SHA-256 do APK em hexadecimal minúsculo: a TV confere antes de instalar.</summary>
    public string Hash { get; set; } = null!;

    public DistribuicaoVersao Distribuicao { get; set; } = DistribuicaoVersao.Suspensa;
}

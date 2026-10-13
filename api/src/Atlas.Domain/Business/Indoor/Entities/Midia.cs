using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Vídeo ou imagem do parceiro. O arquivo fica no storage externo (Supabase);
/// aqui ficam a chave do objeto e o hash que a TV confere depois de baixar.
/// O arquivo nunca muda: trocar o conteúdo é enviar outra mídia.
/// </summary>
[MetadataConfiguration("Mídias")]
public class Midia : BaseAuditableEntity
{
    /// <summary>Limite por arquivo do plano gratuito do Supabase Storage.</summary>
    public const long TamanhoMaximoBytes = 50L * 1024 * 1024;

    public const int DuracaoPadraoSegundos = 15;
    public const int DuracaoMaximaSegundos = 3600;

    public long ParceiroId { get; set; }
    public Parceiro Parceiro { get; set; } = null!;

    public string Titulo { get; set; } = null!;

    public TipoMidia Tipo { get; set; }

    /// <summary>Caminho do objeto no bucket (<c>parceiros/{id}/midias/{guid}.mp4</c>). Único.</summary>
    public string ChaveArmazenamento { get; set; } = null!;

    /// <summary>MIME conferido no envio (<c>video/mp4</c>, <c>image/jpeg</c>, <c>image/png</c>).</summary>
    public string TipoConteudo { get; set; } = null!;

    public long TamanhoBytes { get; set; }

    /// <summary>SHA-256 do arquivo em hexadecimal minúsculo (64 caracteres).</summary>
    public string Hash { get; set; } = null!;

    /// <summary>
    /// Tempo na tela. Para imagem é o que vale; para vídeo é a duração do
    /// arquivo, usada pelo player só como limite se o fim do vídeo não chegar.
    /// </summary>
    public int DuracaoSegundos { get; set; } = DuracaoPadraoSegundos;

    /// <summary>Primeiro dia de exibição (no fuso do parceiro). Nulo = desde já.</summary>
    public DateOnly? InicioExibicao { get; set; }

    /// <summary>Último dia de exibição, inclusive (no fuso do parceiro). Nulo = sem fim.</summary>
    public DateOnly? FimExibicao { get; set; }

    /// <summary>Inativa sai de todas as grades sem perder o vínculo com as playlists.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Se a mídia está ativa e dentro da validade no dia informado.</summary>
    public bool EstaVigente(DateOnly hoje) =>
        Ativo
        && (InicioExibicao is null || InicioExibicao <= hoje)
        && (FimExibicao is null || FimExibicao >= hoje);
}

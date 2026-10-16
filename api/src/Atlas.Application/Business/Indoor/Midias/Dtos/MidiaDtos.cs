using Atlas.Domain.Business.Parceiros.Enums;
using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Midias.Dtos;

public sealed record MidiaDto : IDtoAuditavel
{
    public long Id { get; init; }

    /// <summary>Anunciante dono da mídia (a operadora vê as de todos).</summary>
    public long ParceiroId { get; init; }
    public string ParceiroNome { get; init; } = null!;

    /// <summary>Mídia de parceiro bloqueado não toca em nenhuma TV.</summary>
    public bool ParceiroBloqueado { get; init; }

    public string Titulo { get; init; } = null!;
    public TipoMidia Tipo { get; init; }
    public string TipoConteudo { get; init; } = null!;
    public long TamanhoBytes { get; init; }
    public string Hash { get; init; } = null!;
    public int DuracaoSegundos { get; init; }
    public DateOnly? InicioExibicao { get; init; }
    public DateOnly? FimExibicao { get; init; }
    public bool Ativo { get; init; }

    /// <summary>QR code exibido por cima da mídia (nulo = sem QR code) e o que foi informado em cada campo.</summary>
    public TipoQrCode? QrTipo { get; init; }
    public string? QrDestino { get; init; }
    public string? QrMensagem { get; init; }
    public decimal? QrValor { get; init; }
    public string? QrNomeRecebedor { get; init; }
    public string? QrCidadeRecebedor { get; init; }
    public string? QrLegenda { get; init; }
    public PosicaoQrCode? QrPosicao { get; init; }
    public TamanhoQrCode? QrTamanho { get; init; }

    /// <summary>O texto codificado (link, tel: ou Pix copia e cola), para a prévia no painel.</summary>
    public string? QrConteudo { get; init; }

    /// <summary>Em quantas playlists a mídia aparece (mídia em uso não pode ser excluída).</summary>
    public int QuantidadePlaylists { get; init; }

    /// <summary>Endereço temporário para pré-visualizar no painel. Só no detalhe (<c>GET /{id}</c>).</summary>
    public Uri? UrlVisualizacao { get; init; }

    /// <summary>Há um arquivo novo aguardando a aprovação da operadora (as TVs seguem com o atual).</summary>
    public bool TrocaPendente { get; init; }

    public TipoMidia? PendenteTipo { get; init; }
    public long? PendenteTamanhoBytes { get; init; }
    public DateTimeOffset? PendenteEnviadoEm { get; init; }

    /// <summary>Por que a última troca foi recusada.</summary>
    public string? MotivoRecusaTroca { get; init; }

    /// <summary>Endereço temporário do arquivo pendente (só no detalhe da operadora).</summary>
    public Uri? UrlVisualizacaoPendente { get; init; }

    public DateTimeOffset Created { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset LastModified { get; init; }
    public long? LastModifiedBy { get; init; }
    public string? LastModifiedByName { get; set; }
    public uint Version { get; init; }
}

/// <summary>Onde e até quando o navegador pode enviar o arquivo.</summary>
/// <param name="Chave">Volta no <c>CriarMidiaCommand</c> depois do envio.</param>
/// <param name="UrlEnvio">Destino do <c>PUT</c> com o arquivo no corpo e o <c>Content-Type</c> do arquivo.</param>
/// <param name="ExpiraEm">Até quando a URL aceita o envio (2 h no Supabase).</param>
public sealed record EnvioMidiaDto(string Chave, Uri UrlEnvio, DateTimeOffset ExpiraEm);

/// <summary>Uso do storage pelo parceiro, para o painel mostrar antes do envio.</summary>
/// <summary>Espaço e plano do parceiro, para o painel avisar antes de enviar ou ativar.</summary>
/// <param name="UsadoBytes">Soma das mídias gravadas (ativas ou não).</param>
/// <param name="CotaBytes">Espaço contratado.</param>
/// <param name="TamanhoMaximoArquivoBytes">Limite por arquivo.</param>
/// <param name="MidiasAtivas">Mídias ativas agora.</param>
/// <param name="LimiteMidiasAtivas">Quantas o plano permite ao mesmo tempo.</param>
public sealed record UsoArmazenamentoDto(
    long UsadoBytes,
    long CotaBytes,
    long TamanhoMaximoArquivoBytes,
    int MidiasAtivas,
    int LimiteMidiasAtivas);

public static class MidiaProjecao
{
    public static IQueryable<MidiaDto> ParaDto(this IQueryable<Midia> query, IApplicationDbContext context) =>
        query.Select(m => new MidiaDto
        {
            Id = m.Id,
            ParceiroId = m.ParceiroId,
            ParceiroNome = m.Parceiro.Nome,
            ParceiroBloqueado = m.Parceiro.Situacao == SituacaoParceiro.Bloqueado,
            Titulo = m.Titulo,
            Tipo = m.Tipo,
            TipoConteudo = m.TipoConteudo,
            TamanhoBytes = m.TamanhoBytes,
            Hash = m.Hash,
            DuracaoSegundos = m.DuracaoSegundos,
            InicioExibicao = m.InicioExibicao,
            FimExibicao = m.FimExibicao,
            Ativo = m.Ativo,
            QrTipo = m.QrTipo,
            QrDestino = m.QrDestino,
            QrMensagem = m.QrMensagem,
            QrValor = m.QrValor,
            QrNomeRecebedor = m.QrNomeRecebedor,
            QrCidadeRecebedor = m.QrCidadeRecebedor,
            QrLegenda = m.QrLegenda,
            QrPosicao = m.QrPosicao,
            QrTamanho = m.QrTamanho,
            QrConteudo = m.QrConteudo,
            TrocaPendente = m.PendenteChave != null,
            PendenteTipo = m.PendenteTipo,
            PendenteTamanhoBytes = m.PendenteTamanhoBytes,
            PendenteEnviadoEm = m.PendenteEnviadoEm,
            MotivoRecusaTroca = m.MotivoRecusaTroca,
            QuantidadePlaylists = context.ItensPlaylist.Where(i => i.MidiaId == m.Id).Select(i => i.PlaylistId).Distinct().Count(),
            Created = m.Created,
            CreatedBy = m.CreatedBy,
            LastModified = m.LastModified,
            LastModifiedBy = m.LastModifiedBy,
            Version = EF.Property<uint>(m, "Version"),
        });
}

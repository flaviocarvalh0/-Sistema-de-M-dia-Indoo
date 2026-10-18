using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Clientes;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Vídeo ou imagem do cliente (o criativo do anunciante). O arquivo fica no
/// storage externo (Supabase); aqui ficam a chave do objeto e o hash que a TV
/// confere depois de baixar. Substituir o arquivo mantém a mídia (e as
/// playlists onde ela está): o hash novo faz as TVs baixarem de novo.
/// </summary>
[MetadataConfiguration("Mídias")]
public class Midia : BaseAuditableEntity, IDoCliente
{
    /// <summary>Limite por arquivo do plano gratuito do Supabase Storage.</summary>
    public const long TamanhoMaximoBytes = 50L * 1024 * 1024;

    public const int DuracaoPadraoSegundos = 15;
    public const int DuracaoMaximaSegundos = 3600;

    public long ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public string Titulo { get; set; } = null!;

    public TipoMidia Tipo { get; set; }

    /// <summary>Caminho do objeto no bucket (<c>clientes/{id}/midias/{guid}.mp4</c>). Único.</summary>
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

    /// <summary>Primeiro dia de exibição (no fuso do cliente). Nulo = desde já.</summary>
    public DateOnly? InicioExibicao { get; set; }

    /// <summary>Último dia de exibição, inclusive (no fuso do cliente). Nulo = sem fim.</summary>
    public DateOnly? FimExibicao { get; set; }

    /// <summary>Inativa sai de todas as grades sem perder o vínculo com as playlists.</summary>
    public bool Ativo { get; set; } = true;

    public const int TamanhoMaximoConteudoQrCode = 512;
    public const int TamanhoMaximoLegendaQrCode = 40;

    /// <summary>
    /// QR code exibido por cima da mídia na TV; nulo = sem QR code. Os campos
    /// abaixo guardam o que o anunciante informou (cada tipo usa os seus) e
    /// <see cref="QrConteudo"/>, o texto final codificado, montado pela API.
    /// </summary>
    public TipoQrCode? QrTipo { get; private set; }

    /// <summary>Link, telefone (WhatsApp/ligação), usuário do Instagram ou chave Pix.</summary>
    public string? QrDestino { get; private set; }

    /// <summary>WhatsApp: mensagem inicial. Pix: descrição não é usada.</summary>
    public string? QrMensagem { get; private set; }

    /// <summary>Pix: valor fixo (opcional; sem valor, quem paga digita).</summary>
    public decimal? QrValor { get; private set; }

    /// <summary>Pix: nome e cidade do recebedor (exigidos pelo padrão BR Code).</summary>
    public string? QrNomeRecebedor { get; private set; }
    public string? QrCidadeRecebedor { get; private set; }

    /// <summary>Texto curto embaixo do QR code (ex.: "Peça pelo WhatsApp").</summary>
    public string? QrLegenda { get; private set; }

    public PosicaoQrCode? QrPosicao { get; private set; }
    public TamanhoQrCode? QrTamanho { get; private set; }

    /// <summary>O que vai codificado no QR code (URL, tel: ou o Pix copia e cola).</summary>
    public string? QrConteudo { get; private set; }

    /// <summary>Define o QR code (já validado e montado pela aplicação).</summary>
    public void DefinirQrCode(QrCodeDaMidia qr)
    {
        QrTipo = qr.Tipo;
        QrDestino = qr.Destino;
        QrMensagem = qr.Mensagem;
        QrValor = qr.Valor;
        QrNomeRecebedor = qr.NomeRecebedor;
        QrCidadeRecebedor = qr.CidadeRecebedor;
        QrLegenda = qr.Legenda;
        QrPosicao = qr.Posicao;
        QrTamanho = qr.Tamanho;
        QrConteudo = qr.Conteudo;
    }

    public void RemoverQrCode()
    {
        QrTipo = null;
        QrDestino = QrMensagem = QrNomeRecebedor = QrCidadeRecebedor = QrLegenda = QrConteudo = null;
        QrValor = null;
        QrPosicao = null;
        QrTamanho = null;
    }

    /// <summary>
    /// Troca o arquivo, mantendo a mídia e as playlists onde ela está. A chave
    /// antiga não é apagada aqui: quem chama apaga do storage depois de gravar.
    /// </summary>
    public void SubstituirArquivo(string chave, TipoMidia tipo, string tipoConteudo, long tamanhoBytes, string hash)
    {
        ChaveArmazenamento = chave;
        Tipo = tipo;
        TipoConteudo = tipoConteudo;
        TamanhoBytes = tamanhoBytes;
        Hash = hash;
    }

    // ── Troca de arquivo aguardando aprovação (ADR-020) ─────────────────

    public const int TamanhoMaximoMotivoRecusa = 500;

    /// <summary>
    /// Arquivo novo enviado pelo anunciante para uma mídia que está em alguma
    /// grade. As TVs seguem com o arquivo atual até a operadora aprovar.
    /// Nulo = nenhuma troca pendente.
    /// </summary>
    public string? PendenteChave { get; private set; }

    public TipoMidia? PendenteTipo { get; private set; }
    public string? PendenteTipoConteudo { get; private set; }
    public long? PendenteTamanhoBytes { get; private set; }
    public string? PendenteHash { get; private set; }

    /// <summary>Duração que passa a valer com o arquivo novo; nula = mantém a atual.</summary>
    public int? PendenteDuracaoSegundos { get; private set; }

    public DateTimeOffset? PendenteEnviadoEm { get; private set; }

    /// <summary>Por que a última troca foi recusada (o anunciante vê); some na próxima troca.</summary>
    public string? MotivoRecusaTroca { get; private set; }

    public bool TemTrocaPendente => PendenteChave is not null;

    /// <summary>
    /// Guarda o arquivo novo como pendente. Devolve a chave do pendente
    /// anterior (substituído por este), que quem chama apaga do storage.
    /// </summary>
    public string? ProporArquivo(
        string chave, TipoMidia tipo, string tipoConteudo, long tamanhoBytes, string hash, int? duracaoSegundos, DateTimeOffset agora)
    {
        var anterior = PendenteChave;
        PendenteChave = chave;
        PendenteTipo = tipo;
        PendenteTipoConteudo = tipoConteudo;
        PendenteTamanhoBytes = tamanhoBytes;
        PendenteHash = hash;
        PendenteDuracaoSegundos = duracaoSegundos;
        PendenteEnviadoEm = agora;
        MotivoRecusaTroca = null;
        return anterior;
    }

    /// <summary>A troca vale: o arquivo pendente vira o atual. Devolve a chave antiga, para apagar.</summary>
    public string AprovarTroca()
    {
        if (PendenteChave is null)
        {
            throw new InvalidOperationException("A mídia não tem troca de arquivo pendente.");
        }

        var antiga = ChaveArmazenamento;
        SubstituirArquivo(PendenteChave, PendenteTipo!.Value, PendenteTipoConteudo!, PendenteTamanhoBytes!.Value, PendenteHash!);
        if (PendenteDuracaoSegundos is { } duracao)
        {
            DuracaoSegundos = duracao;
        }

        LimparPendente();
        return antiga;
    }

    /// <summary>A troca não vale: o arquivo atual continua. Devolve a chave do pendente, para apagar.</summary>
    public string RecusarTroca(string motivo)
    {
        if (PendenteChave is null)
        {
            throw new InvalidOperationException("A mídia não tem troca de arquivo pendente.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("O motivo da recusa é obrigatório.");
        }

        var pendente = PendenteChave;
        LimparPendente();
        MotivoRecusaTroca = motivo.Trim();
        return pendente;
    }

    private void LimparPendente()
    {
        PendenteChave = null;
        PendenteTipo = null;
        PendenteTipoConteudo = null;
        PendenteTamanhoBytes = null;
        PendenteHash = null;
        PendenteDuracaoSegundos = null;
        PendenteEnviadoEm = null;
    }

    /// <summary>Se a mídia está ativa e dentro da validade no dia informado.</summary>
    public bool EstaVigente(DateOnly hoje) =>
        Ativo
        && (InicioExibicao is null || InicioExibicao <= hoje)
        && (FimExibicao is null || FimExibicao >= hoje);
}

/// <summary>QR code pronto para gravar: o que foi informado e o <see cref="Conteudo"/> montado.</summary>
public sealed record QrCodeDaMidia(
    TipoQrCode Tipo,
    string Destino,
    string? Mensagem,
    decimal? Valor,
    string? NomeRecebedor,
    string? CidadeRecebedor,
    string? Legenda,
    PosicaoQrCode Posicao,
    TamanhoQrCode Tamanho,
    string Conteudo);

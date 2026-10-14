using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// TV (TV box) instalada num parceiro. O hardware é da operadora: ela
/// cadastra a tela, escolhe o parceiro e pareia a TV (código de uso único que
/// a TV troca por um token próprio). O parceiro só escolhe a
/// <see cref="Playlist"/> que a tela exibe.
/// </summary>
/// <remarks>
/// O que muda a cada sincronização (último contato, versão do app) fica em
/// <see cref="EstadoTela"/>, fora desta tabela: assim o histórico da tela
/// registra só as mudanças feitas no painel e no pareamento.
/// </remarks>
[MetadataConfiguration("Telas")]
public class Tela : BaseAuditableEntity, IDoParceiro
{
    public const int TamanhoCodigoPareamento = 8;

    public static readonly TimeSpan ValidadeCodigoPareamento = TimeSpan.FromHours(24);

    /// <summary>
    /// Sem sincronizar há mais que isto, a tela aparece Offline no painel
    /// (cinco vezes o intervalo padrão de sincronização do player, 60 s).
    /// </summary>
    public static readonly TimeSpan ToleranciaOffline = TimeSpan.FromMinutes(5);

    public long ParceiroId { get; set; }
    public Parceiro Parceiro { get; set; } = null!;

    public string Nome { get; set; } = null!;

    /// <summary>Onde a TV está (ex.: "Recepção", "Sala de musculação").</summary>
    public string? Localizacao { get; set; }

    /// <summary>
    /// MAC, número de série ou etiqueta do aparelho, anotado pela operadora na
    /// instalação. Só informativo: o Android 10+ não deixa o app ler o MAC, e
    /// a identidade da TV é o <see cref="DispositivoId"/>.
    /// </summary>
    public string? IdentificacaoAparelho { get; set; }

    /// <summary>Grade exibida. Nula = TV pareada mostra a tela de espera.</summary>
    public long? PlaylistId { get; set; }
    public Playlist? Playlist { get; set; }

    public bool Ativo { get; set; } = true;

    // ── Pareamento ──────────────────────────────────────────────────────

    /// <summary>Código que a TV digita para parear. Único enquanto existir; some ao parear.</summary>
    public string? CodigoPareamento { get; set; }

    public DateTimeOffset? CodigoPareamentoExpiraEm { get; set; }

    /// <summary>Identificador gerado pelo app na instalação. Nulo = sem TV pareada.</summary>
    public string? DispositivoId { get; set; }

    public DateTimeOffset? PareadaEm { get; set; }

    /// <summary>
    /// Vai no token da TV. Desvincular ou parear outra TV incrementa, e os
    /// tokens emitidos antes deixam de valer sem precisar de lista de revogação.
    /// </summary>
    public int VersaoCredencial { get; set; } = 1;

    public EstadoTela? Estado { get; set; }

    public bool Pareada => DispositivoId is not null;

    public void GerarCodigoPareamento(string codigo, DateTimeOffset agora)
    {
        if (codigo.Length != TamanhoCodigoPareamento)
        {
            throw new InvalidOperationException($"O código de pareamento deve ter {TamanhoCodigoPareamento} caracteres.");
        }

        CodigoPareamento = codigo;
        CodigoPareamentoExpiraEm = agora + ValidadeCodigoPareamento;
    }

    public bool CodigoPareamentoValido(string codigo, DateTimeOffset agora) =>
        Ativo
        && CodigoPareamento is not null
        && CodigoPareamentoExpiraEm > agora
        && string.Equals(CodigoPareamento, codigo, StringComparison.Ordinal);

    /// <summary>Vincula a TV e consome o código. Uma TV pareada antes perde o acesso.</summary>
    public void Parear(string dispositivoId, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(dispositivoId))
        {
            throw new InvalidOperationException("O identificador do dispositivo é obrigatório.");
        }

        DispositivoId = dispositivoId;
        PareadaEm = agora;
        CodigoPareamento = null;
        CodigoPareamentoExpiraEm = null;
        VersaoCredencial++;
    }

    /// <summary>Tira a TV da tela: o token dela para de valer na próxima sincronização.</summary>
    public void Desvincular()
    {
        DispositivoId = null;
        PareadaEm = null;
        VersaoCredencial++;
    }

    /// <summary>
    /// Leva a tela para outro parceiro. A playlist era do parceiro anterior e
    /// sai; a TV pareada e o código pendente também, para nenhuma TV tocar ou
    /// parear com o conteúdo de quem não é mais dono da tela.
    /// </summary>
    public void TransferirPara(long parceiroId)
    {
        if (parceiroId == ParceiroId)
        {
            return;
        }

        ParceiroId = parceiroId;
        PlaylistId = null;
        CodigoPareamento = null;
        CodigoPareamentoExpiraEm = null;

        if (Pareada)
        {
            Desvincular();
        }
    }

    public SituacaoTela Situacao(DateTimeOffset agora, TimeSpan toleranciaOffline) =>
        CalcularSituacao(Pareada, Estado?.UltimaSincronizacao, agora, toleranciaOffline);

    /// <summary>A regra da situação, também usada nas consultas que não carregam a entidade.</summary>
    public static SituacaoTela CalcularSituacao(
        bool pareada,
        DateTimeOffset? ultimaSincronizacao,
        DateTimeOffset agora,
        TimeSpan toleranciaOffline) =>
        !pareada
            ? SituacaoTela.AguardandoPareamento
            : ultimaSincronizacao is { } ultima && agora - ultima <= toleranciaOffline
                ? SituacaoTela.Online
                : SituacaoTela.Offline;
}

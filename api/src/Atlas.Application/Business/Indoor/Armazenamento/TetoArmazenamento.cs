using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>Uso do storage da rede inteira (ADR-025).</summary>
/// <param name="ArquivosBytes">Soma do que o banco conhece: mídias, trocas pendentes, APKs e imagens das notícias.</param>
/// <param name="ReservadoBytes">Envios em andamento (URLs emitidas, arquivo ainda não confirmado).</param>
/// <param name="BucketBytes">O que o último inventário mediu no bucket (nulo sem inventário recente ou sem listagem).</param>
/// <param name="LimiteBytes">Teto configurado (no máximo 10 GB).</param>
/// <param name="InventarioEm">Quando o bucket foi medido pela última vez.</param>
/// <param name="Objetos">Arquivos no bucket no último inventário (nulo se o storage não lista).</param>
/// <param name="OrfaosRemovidos">Arquivos sem dono apagados no último inventário.</param>
/// <param name="BytesOrfaosRemovidos">Espaço liberado por eles.</param>
public sealed record UsoArmazenamentoRedeDto(
    long ArquivosBytes,
    long ReservadoBytes,
    long? BucketBytes,
    long LimiteBytes,
    DateTimeOffset? InventarioEm,
    int? Objetos,
    int OrfaosRemovidos,
    long BytesOrfaosRemovidos)
{
    /// <summary>Os 10 GB do plano gratuito do R2.</summary>
    public const long LimiteGratuitoBytes = 10L * 1024 * 1024 * 1024;

    /// <summary>O maior entre o banco e o bucket: na dúvida, conta o pior caso.</summary>
    public long UsadoBytes => Math.Max(ArquivosBytes, BucketBytes ?? 0);

    /// <summary>Usado + reservado, em % do teto.</summary>
    public int Percentual => LimiteBytes <= 0 ? 100 : (int)Math.Min(100, (UsadoBytes + ReservadoBytes) * 100 / LimiteBytes);

    public long LivreBytes => Math.Max(0, LimiteBytes - UsadoBytes - ReservadoBytes);
}

/// <summary>
/// O teto de armazenamento da rede (ADR-025): nenhuma URL de envio sai se o
/// arquivo não couber, contando o que já está gravado (o maior entre o banco
/// e a última medição do bucket) e os envios em andamento.
/// </summary>
/// <remarks>
/// As outras travas: o tamanho vai assinado na URL (o R2 recusa arquivo maior
/// que o declarado), a API confere o arquivo depois do envio (e apaga se não
/// bater) e o inventário de hora em hora mede o bucket e apaga órfãos.
/// </remarks>
public class TetoArmazenamento(IApplicationDbContext context, IConfiguracaoIndoor configuracao, TimeProvider timeProvider)
{
    /// <summary>Quanto tempo a reserva vale (a mesma validade da URL de envio).</summary>
    public static readonly TimeSpan ValidadeReserva = TimeSpan.FromHours(2);

    /// <summary>Medição do bucket mais velha que isto não conta (o inventário roda de hora em hora).</summary>
    public static readonly TimeSpan ValidadeInventario = TimeSpan.FromHours(3);

    /// <summary>Conteúdo automático (imagens das notícias) só entra até esta fração do teto.</summary>
    public const double FracaoConteudoAutomatico = 0.9;

    /// <summary>Uma reserva por vez neste processo: duas pessoas enviando juntas não passam do teto.</summary>
    private static readonly SemaphoreSlim Trava = new(1, 1);

    public async Task<UsoArmazenamentoRedeDto> MedirAsync(CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow();

        var midias = await context.Midias.SumAsync(m => (long?)m.TamanhoBytes + (m.PendenteTamanhoBytes ?? 0), cancellationToken) ?? 0;
        var apks = await context.VersoesApp.SumAsync(v => (long?)v.TamanhoBytes, cancellationToken) ?? 0;
        var noticias = await context.Noticias.SumAsync(n => n.TamanhoImagemBytes, cancellationToken) ?? 0;
        var reservado = await context.ReservasEnvio.Where(r => r.ExpiraEm > agora).SumAsync(r => (long?)r.TamanhoBytes, cancellationToken) ?? 0;
        var inventario = await context.InventariosArmazenamento.AsNoTracking().FirstOrDefaultAsync(i => i.Id == InventarioArmazenamento.IdUnico, cancellationToken);
        var recente = inventario is { Objetos: not null } && agora - inventario.VerificadoEm <= ValidadeInventario;

        return new UsoArmazenamentoRedeDto(
            midias + apks + noticias,
            reservado,
            recente ? inventario!.BytesNoBucket : null,
            configuracao.LimiteArmazenamentoBytes,
            inventario?.VerificadoEm,
            inventario?.Objetos,
            inventario?.OrfaosRemovidos ?? 0,
            inventario?.BytesOrfaosRemovidos ?? 0);
    }

    /// <summary>
    /// Reserva o espaço do envio ou recusa com a explicação. Chamado antes de
    /// gerar a URL de envio; a reserva some na confirmação ou em 2 h.
    /// </summary>
    /// <exception cref="ValidationException">Não cabe no teto da rede.</exception>
    public async Task ReservarAsync(string chave, long tamanhoBytes, string campo, CancellationToken cancellationToken)
    {
        await Trava.WaitAsync(cancellationToken);
        try
        {
            var uso = await MedirAsync(cancellationToken);
            if (uso.UsadoBytes + uso.ReservadoBytes + tamanhoBytes > uso.LimiteBytes)
            {
                throw new ValidationException(campo,
                [
                    $"O armazenamento da rede está cheio ({Mb(uso.UsadoBytes + uso.ReservadoBytes)} de {Mb(uso.LimiteBytes)}). "
                    + "Fale com a operadora para liberar espaço.",
                ]);
            }

            var agora = timeProvider.GetUtcNow();
            context.ReservasEnvio.RemoveRange(await context.ReservasEnvio.Where(r => r.ExpiraEm <= agora).ToListAsync(cancellationToken));
            context.ReservasEnvio.Add(new ReservaEnvio
            {
                Chave = chave,
                TamanhoBytes = tamanhoBytes,
                CriadaEm = agora,
                ExpiraEm = agora + ValidadeReserva,
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            Trava.Release();
        }
    }

    /// <summary>Conteúdo que a API busca sozinha (imagens das notícias) só entra com folga de 10%.</summary>
    public async Task<bool> CabeConteudoAutomaticoAsync(long tamanhoBytes, CancellationToken cancellationToken)
    {
        var uso = await MedirAsync(cancellationToken);
        return uso.UsadoBytes + uso.ReservadoBytes + tamanhoBytes <= uso.LimiteBytes * FracaoConteudoAutomatico;
    }

    /// <summary>
    /// O arquivo foi conferido e passa a contar no banco: a reserva sai junto
    /// com a gravação de quem chama (mesmo <c>SaveChanges</c>).
    /// </summary>
    public static async Task ConfirmarAsync(IApplicationDbContext context, string chave, CancellationToken cancellationToken)
    {
        var reserva = await context.ReservasEnvio.FirstOrDefaultAsync(r => r.Chave == chave, cancellationToken);
        if (reserva is not null)
        {
            context.ReservasEnvio.Remove(reserva);
        }
    }

    private static string Mb(long bytes) => $"{bytes / (1024.0 * 1024):N0} MB";
}

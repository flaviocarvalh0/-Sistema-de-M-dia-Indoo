using System.Globalization;
using Atlas.Application.Business.Common;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>
/// Inventário do storage (ADR-025), de hora em hora: lista o bucket para medir
/// o uso REAL, apaga os arquivos sem dono (envios abandonados, arquivos de
/// notícias que saíram) e avisa a operadora por e-mail quando o uso passa de
/// 70%, 85% e 95% do teto.
/// </summary>
/// <remarks>
/// Apagar é a parte perigosa, por isso três salvaguardas: só apaga o que
/// nenhuma tabela referencia (mídias, trocas pendentes, APKs, imagens de
/// notícias, envios em andamento), só o que tem mais de
/// <see cref="IdadeMinimaOrfao"/>, e nunca mais da metade dos arquivos numa
/// passada (se parecer que tudo é órfão, algo está errado: só avisa).
/// </remarks>
public class InventarioArmazenamentoServico(
    IApplicationDbContext context,
    IArmazenamentoMidias armazenamento,
    TetoArmazenamento teto,
    IConfiguracaoIndoor configuracao,
    IEmailMessageService emails,
    TimeProvider timeProvider,
    ILogger<InventarioArmazenamentoServico> logger)
{
    /// <summary>Arquivo sem dono mais novo que isto pode ser um envio em andamento: fica.</summary>
    public static readonly TimeSpan IdadeMinimaOrfao = TimeSpan.FromHours(6);

    /// <summary>Níveis de alerta, em % do teto.</summary>
    public static readonly int[] Niveis = [70, 85, 95];

    public sealed record Resultado(long BytesNoBucket, int? Objetos, int OrfaosRemovidos, long BytesOrfaosRemovidos, int Percentual, int? AlertaEnviado);

    public async Task<Resultado> ExecutarAsync(CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow();
        var registro = await context.InventariosArmazenamento.FirstOrDefaultAsync(i => i.Id == InventarioArmazenamento.IdUnico, cancellationToken);
        if (registro is null)
        {
            registro = new InventarioArmazenamento { Id = InventarioArmazenamento.IdUnico };
            context.InventariosArmazenamento.Add(registro);
        }

        long bytes = 0, bytesOrfaos = 0;
        int objetos = 0, orfaos = 0;
        var listado = armazenamento is IListagemArmazenamento;

        if (armazenamento is IListagemArmazenamento listagem)
        {
            var referenciadas = await ChavesReferenciadasAsync(agora, cancellationToken);
            var candidatos = new List<ObjetoListado>();

            await foreach (var objeto in listagem.ListarAsync(cancellationToken))
            {
                objetos++;
                bytes += objeto.TamanhoBytes;
                if (!referenciadas.Contains(objeto.Chave) && agora - objeto.ModificadoEm > IdadeMinimaOrfao)
                {
                    candidatos.Add(objeto);
                }
            }

            if (candidatos.Count > 0 && candidatos.Count * 2 > objetos)
            {
                logger.LogError(
                    "Inventário: {Candidatos} de {Objetos} arquivos parecem sem dono. Nada foi apagado (salvaguarda); confira o banco e o bucket.",
                    candidatos.Count, objetos);
            }
            else
            {
                foreach (var orfao in candidatos)
                {
                    await armazenamento.RemoverAsync(orfao.Chave, cancellationToken);
                    orfaos++;
                    bytesOrfaos += orfao.TamanhoBytes;
                    bytes -= orfao.TamanhoBytes;
                    objetos--;
                }

                if (orfaos > 0)
                {
                    logger.LogInformation("Inventário: {Orfaos} arquivo(s) sem dono apagado(s), {Mb:N1} MB liberados.", orfaos, bytesOrfaos / 1048576.0);
                }
            }
        }
        else
        {
            // Storage sem listagem (Supabase): vale a soma do banco.
            bytes = (await teto.MedirAsync(cancellationToken)).ArquivosBytes;
        }

        registro.VerificadoEm = agora;
        registro.BytesNoBucket = bytes;
        registro.Objetos = listado ? objetos : null;
        registro.OrfaosRemovidos = orfaos;
        registro.BytesOrfaosRemovidos = bytesOrfaos;
        await context.SaveChangesAsync(cancellationToken);

        var uso = await teto.MedirAsync(cancellationToken);
        var nivel = Niveis.Where(n => uso.Percentual >= n).DefaultIfEmpty(0).Max();
        int? alerta = null;

        if (nivel > registro.NivelAlertaAvisado)
        {
            alerta = nivel;
            if (configuracao.EmailsOperadora.Count > 0)
            {
                await emails.EnqueueAsync(Email(uso, nivel), cancellationToken);
            }

            logger.LogWarning("Armazenamento da rede em {Percentual}% do teto ({Usado:N0} de {Limite:N0} MB).",
                uso.Percentual, (uso.UsadoBytes + uso.ReservadoBytes) / 1048576, uso.LimiteBytes / 1048576);
        }

        if (nivel != registro.NivelAlertaAvisado)
        {
            registro.NivelAlertaAvisado = nivel;
            await context.SaveChangesAsync(cancellationToken);
        }

        return new Resultado(bytes, listado ? objetos : null, orfaos, bytesOrfaos, uso.Percentual, alerta);
    }

    private async Task<HashSet<string>> ChavesReferenciadasAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var chaves = new HashSet<string>(StringComparer.Ordinal);
        chaves.UnionWith(await context.Midias.Select(m => m.ChaveArmazenamento).ToListAsync(cancellationToken));
        chaves.UnionWith(await context.Midias.Where(m => m.PendenteChave != null).Select(m => m.PendenteChave!).ToListAsync(cancellationToken));
        chaves.UnionWith(await context.VersoesApp.Select(v => v.ChaveArmazenamento).ToListAsync(cancellationToken));
        chaves.UnionWith(await context.Noticias.Where(n => n.ChaveImagem != null).Select(n => n.ChaveImagem!).ToListAsync(cancellationToken));
        // Envio em andamento (mesmo vencido há pouco): o arquivo pode estar chegando.
        chaves.UnionWith(await context.ReservasEnvio.Select(r => r.Chave).ToListAsync(cancellationToken));
        return chaves;
    }

    private EmailMessageRequest Email(UsoArmazenamentoRedeDto uso, int nivel)
    {
        static string Gb(long bytes) => (bytes / 1073741824.0).ToString("N2", CultureInfo.GetCultureInfo("pt-BR")) + " GB";
        var assunto = $"Armazenamento da rede em {uso.Percentual}% ({Gb(uso.UsadoBytes + uso.ReservadoBytes)} de {Gb(uso.LimiteBytes)})";

        return new EmailMessageRequest
        {
            Subject = assunto,
            HtmlBody = ModeloEmail.Montar(
                previa: assunto,
                titulo: assunto,
                motivo: "Você recebeu este e-mail porque está na lista de avisos da operadora (Indoor:Alertas:Emails).",
                ModeloEmail.Paragrafo(nivel >= 95
                    ? "O armazenamento está quase no teto. Novos envios serão recusados quando não couberem. Libere espaço agora."
                    : "O armazenamento da rede está enchendo. Quando chegar ao teto, novos envios serão recusados."),
                ModeloEmail.Destaque(
                    ("Usado", Gb(uso.UsadoBytes)),
                    ("Envios em andamento", Gb(uso.ReservadoBytes)),
                    ("Teto configurado", Gb(uso.LimiteBytes)),
                    ("Plano gratuito do R2", Gb(UsoArmazenamentoRedeDto.LimiteGratuitoBytes))),
                ModeloEmail.Paragrafo(
                    "Para liberar: exclua mídias que não estão em nenhuma playlist, versões antigas do app (suspensas) "
                    + "ou reduza a cota de clientes que não usam o espaço."),
                ModeloEmail.Botao("Ver uso do armazenamento", $"{configuracao.UrlPainel.TrimEnd('/')}/rede/cliente")),
            Recipients = configuracao.EmailsOperadora
                .Select(e => new EmailRecipientRequest { Email = e, Type = EmailRecipientType.To })
                .ToList(),
            RelatedEntityType = "Armazenamento",
            RelatedEntityId = "1",
        };
    }
}

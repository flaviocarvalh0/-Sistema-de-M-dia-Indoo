using System.Globalization;
using Atlas.Application.Business.Common;
using Atlas.Application.Platform.EmailMessages.Common;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Rede.Telas.Alertas;

/// <summary>
/// Avisa a operadora por e-mail quando uma TV para de sincronizar (ADR-020):
/// um e-mail por varredura com as que caíram e as que voltaram, nunca um por
/// minuto. Só conta TV ativa e pareada, e só dentro do horário de alertas no
/// fuso do estabelecimento (fora dele a loja pode estar fechada; se ainda
/// estiver fora do ar quando o horário abre, o alerta sai nessa hora).
/// </summary>
public class VerificadorTelasOffline(
    IApplicationDbContext context,
    IEmailMessageService emails,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider,
    ILogger<VerificadorTelasOffline> logger)
{
    public sealed record TelaAlerta(long Id, string Nome, string Estabelecimento, DateTimeOffset UltimaSincronizacao);

    /// <summary>O que a varredura avisou.</summary>
    public sealed record Resultado(IReadOnlyList<TelaAlerta> Cairam, IReadOnlyList<TelaAlerta> Voltaram);

    public async Task<Resultado> VerificarAsync(CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow();
        var limite = agora - configuracao.ToleranciaAlertaOffline;
        var (inicio, fim) = configuracao.HorarioAlertas;

        var candidatas = await context.Telas
            .Where(t => t.Ativo && t.DispositivoId != null && t.Estado != null
                && t.Estado.UltimaSincronizacao < limite && t.Estado.AlertaOfflineEm == null)
            .Select(t => new { t.Id, t.Nome, Estabelecimento = t.Parceiro.Nome, t.Parceiro.FusoHorario, t.Estado!.UltimaSincronizacao })
            .ToListAsync(cancellationToken);

        var cairam = candidatas
            .Where(t => FusosHorarios.NoFuso(agora, t.FusoHorario).Hour is var hora && hora >= inicio && hora < fim)
            .Select(t => new TelaAlerta(t.Id, t.Nome, t.Estabelecimento, t.UltimaSincronizacao))
            .ToList();

        // Voltaram: alerta em aberto e sincronizando de novo (ou saíram do monitoramento).
        var voltaram = await context.Telas
            .Where(t => t.Estado != null && t.Estado.AlertaOfflineEm != null
                && (t.Estado.UltimaSincronizacao >= limite || !t.Ativo || t.DispositivoId == null))
            .Select(t => new TelaAlerta(t.Id, t.Nome, t.Parceiro.Nome, t.Estado!.UltimaSincronizacao))
            .ToListAsync(cancellationToken);

        if (cairam.Count == 0 && voltaram.Count == 0)
        {
            return new Resultado([], []);
        }

        var ids = cairam.Select(t => t.Id).Concat(voltaram.Select(t => t.Id)).ToList();
        var estados = await context.EstadosTela.Where(e => ids.Contains(e.TelaId)).ToListAsync(cancellationToken);
        foreach (var estado in estados)
        {
            estado.AlertaOfflineEm = cairam.Any(t => t.Id == estado.TelaId) ? agora : null;
        }

        if (configuracao.EmailsOperadora.Count > 0)
        {
            await emails.EnqueueAsync(Email(cairam, voltaram, agora), cancellationToken);
        }
        else
        {
            logger.LogWarning("{Cairam} TV(s) fora do ar e {Voltaram} de volta, sem e-mail configurado (Indoor:Alertas:Emails).",
                cairam.Count, voltaram.Count);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new Resultado(cairam, voltaram);
    }

    private EmailMessageRequest Email(List<TelaAlerta> cairam, IReadOnlyList<TelaAlerta> voltaram, DateTimeOffset agora)
    {
        var assunto = cairam.Count > 0
            ? $"{cairam.Count} TV(s) fora do ar" + (voltaram.Count > 0 ? $" e {voltaram.Count} de volta" : string.Empty)
            : $"{voltaram.Count} TV(s) de volta ao ar";

        var blocos = new List<string>();
        if (cairam.Count > 0)
        {
            blocos.Add(ModeloEmail.ParagrafoHtml("<strong>Pararam de sincronizar:</strong>"));
            blocos.Add(ModeloEmail.Destaque(cairam
                .Select(t => ($"{t.Nome} · {t.Estabelecimento}", $"desde {ModeloEmail.DataHora(t.UltimaSincronizacao)}"))
                .ToArray()));
            blocos.Add(ModeloEmail.Paragrafo(
                "Verifique se a TV está ligada e com internet. Ela continua tocando o que já baixou, sem receber grade nova."));
        }

        if (voltaram.Count > 0)
        {
            blocos.Add(ModeloEmail.ParagrafoHtml("<strong>Voltaram a sincronizar:</strong>"));
            blocos.Add(ModeloEmail.Destaque(voltaram
                .Select(t => ($"{t.Nome} · {t.Estabelecimento}", $"última sincronização {ModeloEmail.DataHora(t.UltimaSincronizacao)}"))
                .ToArray()));
        }

        blocos.Add(ModeloEmail.Botao("Ver telas", $"{configuracao.UrlPainel.TrimEnd('/')}/rede/tela"));

        return new EmailMessageRequest
        {
            Subject = assunto,
            HtmlBody = ModeloEmail.Montar(
                previa: assunto,
                titulo: assunto,
                motivo: "Você recebeu este e-mail porque está na lista de avisos da operadora (Indoor:Alertas:Emails).",
                [.. blocos]),
            Recipients = configuracao.EmailsOperadora
                .Select(e => new EmailRecipientRequest { Email = e, Type = EmailRecipientType.To })
                .ToList(),
            RelatedEntityType = "Tela",
            RelatedEntityId = (cairam.FirstOrDefault() ?? voltaram[0]).Id.ToString(CultureInfo.InvariantCulture),
        };
    }
}

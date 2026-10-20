using System.Globalization;
using Atlas.Application.Business.Common;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Rede.Telas.Alertas;

/// <summary>
/// Avisa a operadora por e-mail quando uma TV para de sincronizar (ADR-020)
/// ou, sincronizando, para de exibir (heartbeat de reprodução, ADR-024):
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
    public sealed record TelaAlerta(long Id, string Nome, string Estabelecimento, DateTimeOffset UltimaSincronizacao, string? Detalhe = null);

    /// <summary>O que a varredura avisou.</summary>
    public sealed record Resultado(
        IReadOnlyList<TelaAlerta> Cairam,
        IReadOnlyList<TelaAlerta> Voltaram,
        IReadOnlyList<TelaAlerta> PararamDeExibir,
        IReadOnlyList<TelaAlerta> VoltaramAExibir);

    public static string Motivo(MotivoForaDeExibicao? motivo) => motivo switch
    {
        MotivoForaDeExibicao.AppForaDaTela => "o app saiu da tela (outro app aberto ou a TV voltou ao início)",
        MotivoForaDeExibicao.ReproducaoTravada => "a reprodução travou (o item passou muito do tempo dele)",
        MotivoForaDeExibicao.EsperaComAnuncios => "está na tela de espera, mas há anúncios para hoje (arquivos não abrem ou não baixaram)",
        _ => "não está exibindo",
    };

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
            .Select(t => new TelaAlerta(t.Id, t.Nome, t.Parceiro.Nome, t.Estado!.UltimaSincronizacao, null))
            .ToListAsync(cancellationToken);

        // Heartbeat (ADR-024): sincroniza, mas não exibe há mais que a tolerância.
        var limiteExibicao = agora - Tela.ToleranciaForaDeExibicao;
        var candidatasParadas = await context.Telas
            .Where(t => t.Ativo && t.DispositivoId != null && t.Estado != null
                && t.Estado.UltimaSincronizacao >= limite
                && t.Estado.ForaDeExibicaoDesde != null && t.Estado.ForaDeExibicaoDesde <= limiteExibicao
                && t.Estado.AlertaForaDeExibicaoEm == null)
            .Select(t => new
            {
                t.Id, t.Nome, Estabelecimento = t.Parceiro.Nome, t.Parceiro.FusoHorario,
                Desde = t.Estado!.ForaDeExibicaoDesde!.Value, t.Estado.MotivoForaDeExibicao,
            })
            .ToListAsync(cancellationToken);

        var pararam = candidatasParadas
            .Where(t => FusosHorarios.NoFuso(agora, t.FusoHorario).Hour is var hora && hora >= inicio && hora < fim)
            .Select(t => new TelaAlerta(t.Id, t.Nome, t.Estabelecimento, t.Desde, Motivo(t.MotivoForaDeExibicao)))
            .ToList();

        var voltaramAExibir = await context.Telas
            .Where(t => t.Estado != null && t.Estado.AlertaForaDeExibicaoEm != null
                && (t.Estado.ForaDeExibicaoDesde == null || !t.Ativo || t.DispositivoId == null))
            .Select(t => new TelaAlerta(t.Id, t.Nome, t.Parceiro.Nome, t.Estado!.UltimaSincronizacao, null))
            .ToListAsync(cancellationToken);

        if (cairam.Count == 0 && voltaram.Count == 0 && pararam.Count == 0 && voltaramAExibir.Count == 0)
        {
            return new Resultado([], [], [], []);
        }

        var ids = cairam.Concat(voltaram).Concat(pararam).Concat(voltaramAExibir).Select(t => t.Id).Distinct().ToList();
        var estados = await context.EstadosTela.Where(e => ids.Contains(e.TelaId)).ToListAsync(cancellationToken);
        foreach (var estado in estados)
        {
            if (cairam.Any(t => t.Id == estado.TelaId))
            {
                estado.AlertaOfflineEm = agora;
            }
            else if (voltaram.Any(t => t.Id == estado.TelaId))
            {
                estado.AlertaOfflineEm = null;
            }

            if (pararam.Any(t => t.Id == estado.TelaId))
            {
                estado.AlertaForaDeExibicaoEm = agora;
            }
            else if (voltaramAExibir.Any(t => t.Id == estado.TelaId))
            {
                estado.AlertaForaDeExibicaoEm = null;
            }
        }

        if (configuracao.EmailsOperadora.Count > 0)
        {
            await emails.EnqueueAsync(Email(cairam, voltaram, pararam, voltaramAExibir), cancellationToken);
        }
        else
        {
            logger.LogWarning(
                "{Cairam} TV(s) fora do ar, {Voltaram} de volta, {Pararam} sem exibir e {Exibindo} exibindo de novo, sem e-mail configurado (Indoor:Alertas:Emails).",
                cairam.Count, voltaram.Count, pararam.Count, voltaramAExibir.Count);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new Resultado(cairam, voltaram, pararam, voltaramAExibir);
    }

    private EmailMessageRequest Email(
        List<TelaAlerta> cairam,
        IReadOnlyList<TelaAlerta> voltaram,
        List<TelaAlerta> pararam,
        IReadOnlyList<TelaAlerta> voltaramAExibir)
    {
        // "1 TV(s) fora do ar e 2 de volta", "1 TV(s) sem exibir": "TV(s)" só na primeira parte.
        var partes = new List<string>();
        void Parte(int quantidade, string texto)
        {
            if (quantidade > 0) partes.Add(partes.Count == 0 ? $"{quantidade} TV(s) {texto}" : $"{quantidade} {texto}");
        }

        Parte(cairam.Count, "fora do ar");
        Parte(pararam.Count, "sem exibir");
        Parte(voltaram.Count, partes.Count == 0 ? "de volta ao ar" : "de volta");
        Parte(voltaramAExibir.Count, "exibindo de novo");
        var assunto = partes.Count == 1 ? partes[0] : string.Join(", ", partes[..^1]) + " e " + partes[^1];

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

        if (pararam.Count > 0)
        {
            blocos.Add(ModeloEmail.ParagrafoHtml("<strong>Sincronizam, mas não estão exibindo:</strong>"));
            blocos.Add(ModeloEmail.Destaque(pararam
                .Select(t => ($"{t.Nome} · {t.Estabelecimento}", $"desde {ModeloEmail.DataHora(t.UltimaSincronizacao)}: {t.Detalhe}"))
                .ToArray()));
            blocos.Add(ModeloEmail.Paragrafo(
                "A TV tem internet, mas os anúncios não estão passando. Peça para alguém no local abrir o Mídia Indoor ou reiniciar a TV."));
        }

        if (voltaramAExibir.Count > 0)
        {
            blocos.Add(ModeloEmail.ParagrafoHtml("<strong>Voltaram a exibir:</strong>"));
            blocos.Add(ModeloEmail.Destaque(voltaramAExibir
                .Select(t => ($"{t.Nome} · {t.Estabelecimento}", "exibindo normalmente"))
                .ToArray()));
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
            RelatedEntityId = cairam.Concat(pararam).Concat(voltaram).Concat(voltaramAExibir).First().Id.ToString(CultureInfo.InvariantCulture),
        };
    }
}

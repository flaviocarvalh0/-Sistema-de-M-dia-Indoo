using Atlas.Domain.Business.Clientes.Enums;
using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Clientes.Entities;

/// <summary>
/// Cliente atendido pela rede de mídia indoor — o inquilino (academia,
/// tapiocaria, clínica). Tudo que é do cliente (telas, mídias, playlists,
/// equipe) referencia esta entidade. No MVP só a operadora cadastra clientes.
/// </summary>
[MetadataConfiguration("Clientes")]
public class Cliente : BaseAuditableEntity
{
    /// <summary>Cota padrão de armazenamento de mídias, em megabytes.</summary>
    public const int CotaPadraoMb = 200;

    /// <summary>Mídias ativas ao mesmo tempo no plano padrão.</summary>
    public const int LimiteMidiasAtivasPadrao = 1;

    public string Nome { get; set; } = null!;

    public string? NomeResponsavel { get; set; }
    public string Email { get; set; } = null!;

    /// <summary>Só dígitos, com DDD: 10 (fixo) ou 11 (celular).</summary>
    public string? Telefone { get; set; }

    /// <summary>CPF (11) ou CNPJ (14) sem pontuação; CNPJ pode ser alfanumérico. Opcional.</summary>
    public string? Documento { get; set; }

    public string? Cidade { get; set; }

    /// <summary>
    /// Fuso IANA do cliente. Define o "hoje" e o dia da semana com que a grade
    /// de cada tela é resolvida — uma TV às 23h30 de domingo em Manaus não pode
    /// receber a grade de segunda.
    /// </summary>
    public string FusoHorario { get; set; } = "America/Sao_Paulo";

    /// <summary>Espaço máximo somado das mídias do cliente no storage.</summary>
    public int CotaArmazenamentoMb { get; set; } = CotaPadraoMb;

    /// <summary>
    /// Quantas mídias o cliente pode ter ativas ao mesmo tempo (o plano dele).
    /// Vale ao ativar: quem já estava acima quando o limite mudou continua
    /// como está até inativar alguma.
    /// </summary>
    public int LimiteMidiasAtivas { get; set; } = LimiteMidiasAtivasPadrao;

    public SituacaoCliente Situacao { get; set; } = SituacaoCliente.Ativo;

    public string? MotivoBloqueio { get; set; }

    /// <summary>Anotação da operadora; nunca exibida ao cliente.</summary>
    public string? ObservacaoInterna { get; set; }

    // ── Perfil público (diretório das TVs, ADR-018) ─────────────────────

    public const int TamanhoMaximoCategoria = 40;
    public const int TamanhoMaximoDescricaoPublica = 140;

    /// <summary>
    /// O próprio cliente aceitou aparecer na página pública das TVs onde os
    /// anúncios dele passam (padrão: não). Só o dono do cliente muda.
    /// </summary>
    public bool ExibirNoDiretorioPublico { get; private set; }

    /// <summary>Quando o aceite foi dado (registro do consentimento); nulo sem aceite.</summary>
    public DateTimeOffset? DiretorioAceitoEm { get; private set; }

    /// <summary>Ramo exibido no cartão (ex.: "Padaria").</summary>
    public string? Categoria { get; set; }

    /// <summary>Uma frase sobre o negócio, no cartão.</summary>
    public string? DescricaoPublica { get; set; }

    /// <summary>WhatsApp público, só dígitos com o código do país (5592999990000).</summary>
    public string? WhatsappPublico { get; set; }

    /// <summary>Usuário do Instagram, sem o @.</summary>
    public string? InstagramPublico { get; set; }

    /// <summary>Site (URL absoluta http/https).</summary>
    public string? SitePublico { get; set; }

    /// <summary>Liga ou desliga a exibição; ligar registra o momento do aceite.</summary>
    public void DefinirExibicaoNoDiretorio(bool exibir, DateTimeOffset agora)
    {
        if (exibir && !ExibirNoDiretorioPublico)
        {
            DiretorioAceitoEm = agora;
        }
        else if (!exibir)
        {
            DiretorioAceitoEm = null;
        }

        ExibirNoDiretorioPublico = exibir;
    }

    public bool Bloqueado => Situacao == SituacaoCliente.Bloqueado;

    public long CotaArmazenamentoBytes => CotaArmazenamentoMb * 1024L * 1024L;

    public void Bloquear(string motivo)
    {
        if (Bloqueado)
        {
            throw new InvalidOperationException("O cliente já está bloqueado.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("O motivo do bloqueio é obrigatório.");
        }

        Situacao = SituacaoCliente.Bloqueado;
        MotivoBloqueio = motivo.Trim();
    }

    public void Desbloquear()
    {
        if (!Bloqueado)
        {
            throw new InvalidOperationException("O cliente não está bloqueado.");
        }

        Situacao = SituacaoCliente.Ativo;
        MotivoBloqueio = null;
    }
}

using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Parceiros.Entities;

/// <summary>
/// Estabelecimento onde a operadora instala TVs (academia, clínica,
/// tapiocaria). Cede o espaço e <b>não acessa o sistema</b>: quem anuncia é o
/// <see cref="Cliente"/>. Se o parceiro também quiser anunciar, ele é
/// cadastrado como cliente e ligado aqui por <see cref="ClienteId"/>
/// (opcional) — assim o diretório da TV mostra o dono do local primeiro.
/// </summary>
[MetadataConfiguration("Parceiros")]
public class Parceiro : BaseAuditableEntity
{
    public string Nome { get; set; } = null!;

    public string? NomeResponsavel { get; set; }

    /// <summary>Só dígitos, com DDD: 10 (fixo) ou 11 (celular).</summary>
    public string? Telefone { get; set; }

    /// <summary>E-mail de contato (não é login: o parceiro não acessa o sistema).</summary>
    public string? Email { get; set; }

    /// <summary>CPF (11) ou CNPJ (14) sem pontuação; CNPJ pode ser alfanumérico. Opcional.</summary>
    public string? Documento { get; set; }

    public string? Endereco { get; set; }

    /// <summary>Cidade do clima das TVs instaladas aqui (Open-Meteo).</summary>
    public string? Cidade { get; set; }

    /// <summary>
    /// Fuso IANA do local. Define o "hoje" e o dia da semana com que a grade
    /// de cada tela é resolvida — uma TV às 23h30 de domingo em Manaus não pode
    /// receber a grade de segunda.
    /// </summary>
    public string FusoHorario { get; set; } = "America/Sao_Paulo";

    /// <summary>Inativo some das listas de escolha; as TVs dele seguem como estão.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Anotação da operadora (acordo, contato do gerente, horário de funcionamento).</summary>
    public string? Observacao { get; set; }

    /// <summary>O cadastro de cliente do dono do local, quando ele também anuncia.</summary>
    public long? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
}

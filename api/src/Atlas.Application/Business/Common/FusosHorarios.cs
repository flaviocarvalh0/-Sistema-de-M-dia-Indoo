namespace Atlas.Application.Business.Common;

/// <summary>
/// Fuso IANA do parceiro (ex.: <c>America/Manaus</c>). É com ele que o "hoje"
/// e o dia da semana da grade de cada TV são calculados.
/// </summary>
public static class FusosHorarios
{
    public const string Padrao = "America/Sao_Paulo";

    /// <summary>Só identificadores IANA (o Windows também os resolve desde o .NET 6 com ICU).</summary>
    public static bool Valido(string? fuso) =>
        !string.IsNullOrWhiteSpace(fuso)
        && fuso.Contains('/')
        && TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out _);

    /// <summary>Data local do parceiro no instante informado.</summary>
    public static DateTimeOffset NoFuso(DateTimeOffset instante, string fuso) =>
        TimeZoneInfo.ConvertTime(instante, TimeZoneInfo.FindSystemTimeZoneById(fuso));
}

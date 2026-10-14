namespace Atlas.Application.Business.Conteudo.Clima;

/// <summary>
/// Códigos de tempo da WMO (os que o Open-Meteo devolve em <c>weather_code</c>)
/// em português, com um ícone genérico que a TV desenha.
/// </summary>
public static class TempoWmo
{
    /// <returns>Descrição e ícone (<c>sol</c>, <c>sol-nuvem</c>, <c>nuvem</c>, <c>neblina</c>, <c>garoa</c>, <c>chuva</c>, <c>neve</c>, <c>tempestade</c>).</returns>
    public static (string Descricao, string Icone) Descrever(int codigo) => codigo switch
    {
        0 => ("Céu limpo", "sol"),
        1 => ("Predomínio de sol", "sol-nuvem"),
        2 => ("Parcialmente nublado", "sol-nuvem"),
        3 => ("Nublado", "nuvem"),
        45 or 48 => ("Neblina", "neblina"),
        51 or 53 or 55 => ("Garoa", "garoa"),
        56 or 57 => ("Garoa congelante", "garoa"),
        61 => ("Chuva fraca", "chuva"),
        63 => ("Chuva", "chuva"),
        65 => ("Chuva forte", "chuva"),
        66 or 67 => ("Chuva congelante", "chuva"),
        71 or 73 or 75 or 77 or 85 or 86 => ("Neve", "neve"),
        80 => ("Pancadas de chuva fracas", "chuva"),
        81 => ("Pancadas de chuva", "chuva"),
        82 => ("Pancadas de chuva fortes", "chuva"),
        95 => ("Trovoadas", "tempestade"),
        96 or 99 => ("Trovoadas com granizo", "tempestade"),
        _ => ("Tempo indefinido", "nuvem"),
    };
}

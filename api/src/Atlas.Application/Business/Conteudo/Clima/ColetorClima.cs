using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Conteudo.Common;
using Atlas.Domain.Business.Conteudo.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Conteudo.Clima;

/// <summary>
/// Atualiza o tempo na cidade de cada parceiro que hospeda telas ativas,
/// pelo Open-Meteo (gratuito, sem chave; licença CC BY, crédito na TV).
/// As coordenadas vêm da geocodificação da cidade do cadastro e só são
/// buscadas de novo quando a cidade muda.
/// </summary>
/// <remarks>
/// A cidade aceita a UF para desempatar homônimos: "São José - SC",
/// "São José/SC" ou "São José, SC". Sem UF, vale o primeiro resultado no Brasil.
/// </remarks>
public sealed partial class ColetorClima(
    IApplicationDbContext context,
    IClienteConteudoExterno cliente,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider,
    ILogger<ColetorClima> logger)
{
    private const long TamanhoMaximoRespostaBytes = 256 * 1024;

    public async Task AtualizarTodosAsync(CancellationToken cancellationToken)
    {
        var hosts = await context.Telas
            .Where(t => t.Ativo && t.Parceiro.Cidade != null && t.Parceiro.Cidade != "")
            .Select(t => new { t.ParceiroId, t.Parceiro.Cidade })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var host in hosts)
        {
            await AtualizarAsync(host.ParceiroId, host.Cidade!.Trim(), cancellationToken);
        }
    }

    public async Task AtualizarAsync(long parceiroId, string cidade, CancellationToken cancellationToken)
    {
        var previsao = await context.PrevisoesClima.FirstOrDefaultAsync(p => p.ParceiroId == parceiroId, cancellationToken);
        if (previsao is null)
        {
            previsao = new PrevisaoClima { ParceiroId = parceiroId, CidadeConsultada = cidade };
            context.PrevisoesClima.Add(previsao);
        }

        try
        {
            if (previsao.Latitude is null || !string.Equals(previsao.CidadeConsultada, cidade, StringComparison.OrdinalIgnoreCase))
            {
                await GeocodificarAsync(previsao, cidade, cancellationToken);
            }

            if (previsao.Latitude is { } latitude && previsao.Longitude is { } longitude)
            {
                await LerPrevisaoAsync(previsao, latitude, longitude, cancellationToken);
                previsao.UltimoErro = null;
            }
        }
        catch (Exception ex) when (ex is ConteudoExternoIndisponivelException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("Clima de {Cidade} (parceiro {ParceiroId}) não atualizado: {Erro}", cidade, parceiroId, ex.Message);
            previsao.UltimoErro = Cortar(ex.Message);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task GeocodificarAsync(PrevisaoClima previsao, string cidade, CancellationToken cancellationToken)
    {
        var (nome, uf) = SepararUf(cidade);
        var url = new Uri($"{configuracao.UrlGeocodificacao}?name={Uri.EscapeDataString(nome)}&count=10&language=pt&format=json&countryCode=BR");

        using var json = await LerJsonAsync(url, cancellationToken);

        previsao.CidadeConsultada = cidade;
        previsao.Latitude = previsao.Longitude = null;
        previsao.Local = null;
        previsao.TemperaturaC = previsao.MinimaC = previsao.MaximaC = null;
        previsao.CodigoTempo = null;
        previsao.PrevisaoDias = null;
        previsao.AtualizadaEm = null;

        if (!json.RootElement.TryGetProperty("results", out var resultados) || resultados.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Cidade \"{cidade}\" não encontrada. Confira o cadastro do parceiro (ex.: \"São José - SC\").");
        }

        var escolhido = resultados.EnumerateArray()
            .FirstOrDefault(r => uf is null || (r.TryGetProperty("admin1", out var estado) && Estados.TryGetValue(uf, out var nomeEstado)
                && string.Equals(estado.GetString(), nomeEstado, StringComparison.OrdinalIgnoreCase)));

        if (escolhido.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Cidade \"{cidade}\" não encontrada nessa UF.");
        }

        previsao.Latitude = escolhido.GetProperty("latitude").GetDouble();
        previsao.Longitude = escolhido.GetProperty("longitude").GetDouble();
        var nomeLocal = escolhido.GetProperty("name").GetString();
        previsao.Local = escolhido.TryGetProperty("admin1", out var admin1) ? $"{nomeLocal}, {admin1.GetString()}" : nomeLocal;
    }

    private async Task LerPrevisaoAsync(PrevisaoClima previsao, double latitude, double longitude, CancellationToken cancellationToken)
    {
        var url = new Uri(string.Create(CultureInfo.InvariantCulture,
            $"{configuracao.UrlPrevisaoClima}?latitude={latitude:0.####}&longitude={longitude:0.####}&current=temperature_2m,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days={DiasPrevisao}"));

        using var json = await LerJsonAsync(url, cancellationToken);
        var atual = json.RootElement.GetProperty("current");
        var diario = json.RootElement.GetProperty("daily");

        previsao.TemperaturaC = Math.Round(atual.GetProperty("temperature_2m").GetDecimal(), 1);
        previsao.CodigoTempo = atual.GetProperty("weather_code").GetInt32();
        previsao.MaximaC = PrimeiroValor(diario, "temperature_2m_max");
        previsao.MinimaC = PrimeiroValor(diario, "temperature_2m_min");
        previsao.PrevisaoDias = JsonSerializer.Serialize(LerDias(diario));
        previsao.AtualizadaEm = timeProvider.GetUtcNow();
    }

    /// <summary>Hoje e os próximos dias (cartão "Previsão do tempo").</summary>
    public const int DiasPrevisao = 4;

    private static List<DiaPrevisao> LerDias(JsonElement diario)
    {
        var dias = new List<DiaPrevisao>();
        if (!diario.TryGetProperty("time", out var datas))
        {
            return dias;
        }

        for (var i = 0; i < datas.GetArrayLength(); i++)
        {
            if (!DateOnly.TryParse(datas[i].GetString(), CultureInfo.InvariantCulture, out var data)
                || Valor(diario, "temperature_2m_min", i) is not { } minima
                || Valor(diario, "temperature_2m_max", i) is not { } maxima
                || Valor(diario, "weather_code", i) is not { } codigo)
            {
                continue;
            }

            dias.Add(new DiaPrevisao(data, minima, maxima, (int)codigo));
        }

        return dias;
    }

    private static decimal? Valor(JsonElement diario, string campo, int indice) =>
        diario.TryGetProperty(campo, out var valores) && indice < valores.GetArrayLength() && valores[indice].ValueKind == JsonValueKind.Number
            ? Math.Round(valores[indice].GetDecimal(), 1)
            : null;

    /// <summary>Lê o JSON de <see cref="PrevisaoClima.PrevisaoDias"/>; inválido ou vazio vira lista vazia.</summary>
    public static IReadOnlyList<DiaPrevisao> DiasDe(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<DiaPrevisao>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static decimal? PrimeiroValor(JsonElement diario, string campo) =>
        diario.TryGetProperty(campo, out var valores) && valores.GetArrayLength() > 0 && valores[0].ValueKind == JsonValueKind.Number
            ? Math.Round(valores[0].GetDecimal(), 1)
            : null;

    private async Task<JsonDocument> LerJsonAsync(Uri url, CancellationToken cancellationToken)
    {
        var resposta = await cliente.BaixarAsync(url, TamanhoMaximoRespostaBytes, cancellationToken);
        return JsonDocument.Parse(resposta.Bytes);
    }

    /// <summary>"São José - SC" → ("São José", "SC"); sem UF reconhecida, a cidade inteira.</summary>
    public static (string Nome, string? Uf) SepararUf(string cidade)
    {
        var m = CidadeComUf().Match(cidade.Trim());
        return m.Success && Estados.ContainsKey(m.Groups["uf"].Value.ToUpperInvariant())
            ? (m.Groups["nome"].Value.Trim(), m.Groups["uf"].Value.ToUpperInvariant())
            : (cidade.Trim(), null);
    }

    private static string Cortar(string texto) => texto.Length <= 500 ? texto : texto[..500];

    [GeneratedRegex(@"^(?<nome>.+?)\s*[-/,]\s*(?<uf>[A-Za-z]{2})$")]
    private static partial Regex CidadeComUf();

    /// <summary>UF → nome do estado como o Open-Meteo devolve em <c>admin1</c>.</summary>
    private static readonly Dictionary<string, string> Estados = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = "Acre", ["AL"] = "Alagoas", ["AP"] = "Amapá", ["AM"] = "Amazonas", ["BA"] = "Bahia",
        ["CE"] = "Ceará", ["DF"] = "Distrito Federal", ["ES"] = "Espírito Santo", ["GO"] = "Goiás",
        ["MA"] = "Maranhão", ["MT"] = "Mato Grosso", ["MS"] = "Mato Grosso do Sul", ["MG"] = "Minas Gerais",
        ["PA"] = "Pará", ["PB"] = "Paraíba", ["PR"] = "Paraná", ["PE"] = "Pernambuco", ["PI"] = "Piauí",
        ["RJ"] = "Rio de Janeiro", ["RN"] = "Rio Grande do Norte", ["RS"] = "Rio Grande do Sul",
        ["RO"] = "Rondônia", ["RR"] = "Roraima", ["SC"] = "Santa Catarina", ["SP"] = "São Paulo",
        ["SE"] = "Sergipe", ["TO"] = "Tocantins",
    };
}

/// <summary>Um dia da previsão: mínima, máxima e o código WMO do tempo.</summary>
public sealed record DiaPrevisao(DateOnly Data, decimal MinimaC, decimal MaximaC, int Codigo);

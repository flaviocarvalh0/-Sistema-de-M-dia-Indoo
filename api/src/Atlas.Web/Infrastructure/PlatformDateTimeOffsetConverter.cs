using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Web.Infrastructure;

public class PlatformDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private readonly bool _forceTimeZone;
    private readonly TimeZoneInfo _targetTimeZone;

    public PlatformDateTimeOffsetConverter(IConfiguration configuration)
    {
        var section = configuration.GetSection("PlatformSettings");
        _forceTimeZone = section.GetValue<bool>("ForceLocalTimeZone");

        var timeZoneId = section.GetValue<string>("DefaultTimeZoneId") ?? "UTC";

        try
        {
            // Tenta buscar o fuso de forma cross-platform (funciona em Windows e Linux)
            _targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            // Fallback seguro caso o ID esteja incorreto
            _targetTimeZone = TimeZoneInfo.Local;
        }
    }

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateText = reader.GetString();
        return DateTimeOffset.Parse(dateText!);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        if (_forceTimeZone)
        {
            // Converte o instante de tempo para o fuso horário configurado no appsettings
            var convertedTime = TimeZoneInfo.ConvertTime(value, _targetTimeZone);
            writer.WriteStringValue(convertedTime);
        }
        else
        {
            // Mantém o comportamento original (UTC)
            writer.WriteStringValue(value);
        }
    }
}

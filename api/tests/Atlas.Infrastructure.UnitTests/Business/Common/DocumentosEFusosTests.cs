using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Common;
using FluentAssertions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Common;

public class DocumentosBrasilTests
{
    [Theory]
    [InlineData("(11) 99999-0000", true)]
    [InlineData("1133334444", true)]
    [InlineData("11 3333-4444", true)]
    [InlineData("11899990000", false)] // 11 dígitos sem o 9 do celular
    [InlineData("0199990000", false)]  // DDD começando com 0
    [InlineData("999990000", false)]   // sem DDD
    [InlineData("119999900001", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TelefoneValido(string? telefone, bool esperado)
    {
        DocumentosBrasil.TelefoneValido(telefone).Should().Be(esperado);
    }

    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-24", false)] // dígito verificador errado
    [InlineData("111.111.111-11", false)] // todos iguais
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("12.ABC.345/01DE-35", true)]  // CNPJ alfanumérico (exemplo da Receita)
    [InlineData("12.abc.345/01de-35", true)]
    [InlineData("12.ABC.345/01DE-36", false)]
    [InlineData("11.222.333/0001-82", false)]
    [InlineData("5299822472", false)]         // 10 posições
    [InlineData("ABC.982.247-25", false)]     // CPF não tem letra
    [InlineData("", false)]
    [InlineData(null, false)]
    public void DocumentoValido_AceitaCpfOuCnpj(string? documento, bool esperado)
    {
        DocumentosBrasil.DocumentoValido(documento).Should().Be(esperado);
    }

    [Theory]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData(" ./- ", null)]
    [InlineData(null, null)]
    public void NormalizarDocumento(string? documento, string? esperado)
    {
        DocumentosBrasil.NormalizarDocumento(documento).Should().Be(esperado);
    }
}

public class FusosHorariosTests
{
    [Theory]
    [InlineData("America/Sao_Paulo", true)]
    [InlineData("America/Manaus", true)]
    [InlineData("America/Noronha", true)]
    [InlineData("UTC", false)]                              // sem região: não é fuso de parceiro
    [InlineData("E. South America Standard Time", false)]   // id do Windows
    [InlineData("America/Atlantida", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Valido_AceitaSoIdentificadoresIana(string? fuso, bool esperado)
    {
        FusosHorarios.Valido(fuso).Should().Be(esperado);
    }

    [Fact]
    public void NoFuso_DeveDarODiaLocalDoParceiro()
    {
        // 02:30 UTC de segunda ainda é domingo em Manaus (UTC-4).
        var instante = new DateTimeOffset(2026, 10, 12, 2, 30, 0, TimeSpan.Zero);

        var local = FusosHorarios.NoFuso(instante, "America/Manaus");

        local.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        DateOnly.FromDateTime(local.DateTime).Should().Be(new DateOnly(2026, 10, 11));
    }
}

public class GeradorSenhaTemporariaTests
{
    [Fact]
    public void Gerar_DeveAtenderAPoliticaDeSenha()
    {
        for (var i = 0; i < 50; i++)
        {
            var senha = GeradorSenhaTemporaria.Gerar();

            senha.Should().HaveLength(12);
            senha.Should().Match(s => s.Any(char.IsAsciiLetterLower) && s.Any(char.IsAsciiLetterUpper) && s.Any(char.IsAsciiDigit));
            senha.Should().NotContainAny("0", "O", "1", "l", "I");
        }
    }
}

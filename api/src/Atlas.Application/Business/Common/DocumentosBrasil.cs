namespace Atlas.Application.Business.Common;

/// <summary>
/// Normalização e validação de telefone, CPF e CNPJ brasileiros. Gravamos sem
/// pontuação; máscara é assunto da tela.
/// </summary>
public static class DocumentosBrasil
{
    public static string SomenteDigitos(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    /// <summary>
    /// Telefone com DDD: 10 dígitos (fixo) ou 11 (celular, começando com 9
    /// depois do DDD). DDD de 11 a 99.
    /// </summary>
    public static bool TelefoneValido(string? valor)
    {
        var digitos = SomenteDigitos(valor);

        if (digitos.Length is not (10 or 11) || digitos[0] == '0' || digitos[1] == '0')
        {
            return false;
        }

        return digitos.Length == 10 || digitos[2] == '9';
    }

    /// <summary>
    /// CPF ou CNPJ como é gravado: sem pontuação, letras em maiúscula. <c>null</c>
    /// se vier vazio.
    /// </summary>
    public static string? NormalizarDocumento(string? valor)
    {
        var documento = new string((valor ?? string.Empty)
            .Select(char.ToUpperInvariant)
            .Where(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c))
            .ToArray());

        return documento.Length == 0 ? null : documento;
    }

    /// <summary>CPF (11 dígitos) ou CNPJ (14 posições, numérico ou alfanumérico).</summary>
    public static bool DocumentoValido(string? valor) =>
        NormalizarDocumento(valor) is { } documento
        && (documento.Length == 11 ? CpfValido(documento) : CnpjValido(documento));

    public static bool CpfValido(string? valor)
    {
        var cpf = NormalizarDocumento(valor);

        if (cpf is not { Length: 11 } || !cpf.All(char.IsAsciiDigit) || cpf.Distinct().Count() == 1)
        {
            return false;
        }

        return DigitoVerificadorCpf(cpf[..9]) == cpf[9] - '0'
            && DigitoVerificadorCpf(cpf[..10]) == cpf[10] - '0';
    }

    /// <summary>
    /// CNPJ numérico ou alfanumérico (em vigor desde julho de 2026): 12
    /// posições com letra ou dígito e 2 dígitos verificadores. O cálculo é o
    /// mesmo módulo 11 de sempre, com cada caractere valendo o código ASCII
    /// menos 48 — dígito vale ele mesmo, "A" vale 17.
    /// </summary>
    public static bool CnpjValido(string? valor)
    {
        var cnpj = NormalizarDocumento(valor);

        if (cnpj is not { Length: 14 }
            || !char.IsAsciiDigit(cnpj[12])
            || !char.IsAsciiDigit(cnpj[13])
            || cnpj.Distinct().Count() == 1)
        {
            return false;
        }

        return DigitoVerificadorCnpj(cnpj[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) == cnpj[12] - '0'
            && DigitoVerificadorCnpj(cnpj[..13], [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) == cnpj[13] - '0';
    }

    private static int DigitoVerificadorCpf(string digitos)
    {
        var peso = digitos.Length + 1;
        var soma = digitos.Select((c, i) => (c - '0') * (peso - i)).Sum();
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int DigitoVerificadorCnpj(string caracteres, int[] pesos)
    {
        var soma = caracteres.Select((c, i) => (c - '0') * pesos[i]).Sum();
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}

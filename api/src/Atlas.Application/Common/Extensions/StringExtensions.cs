namespace Atlas.Application.Common.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Converte um nome de tabela em formato snake_case no singular para PascalCase no plural,
    /// ideal para geração de chaves de permissão (Claims).
    /// </summary>
    /// <example>
    /// "produto_servico" vira "ProdutoServicos"
    /// "empresa" vira "Empresas"
    /// "configuracao_sistema" vira "ConfiguracaoSistemas"
    /// </example>
    public static string ToPascalCasePlural(this string snakeCaseTable)
    {
        if (string.IsNullOrEmpty(snakeCaseTable)) return string.Empty;

        // 1. Transforma produto_servico em ProdutoServico
        var pascal = string.Join("", snakeCaseTable
            .Split('_')
            .Select(word => word.Length > 0
                ? char.ToUpper(word[0]) + word.Substring(1).ToLowerInvariant()
                : string.Empty));

        // 2. Regra simples de pluralização adaptada para português
        if (pascal.EndsWith("ao", StringComparison.OrdinalIgnoreCase))
            return pascal.Substring(0, pascal.Length - 2) + "oes";

        if (!pascal.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return pascal + "s";

        return pascal;
    }
}

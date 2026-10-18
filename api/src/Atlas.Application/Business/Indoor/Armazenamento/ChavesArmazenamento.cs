namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>
/// Chave do objeto no bucket: <c>clientes/{clienteId}/midias/{guid}.{ext}</c>.
/// Gerada sempre pela API — o painel nunca escolhe onde grava — e conferida
/// quando volta, para um cliente não registrar como sua a mídia de outro.
/// </summary>
public static class ChavesArmazenamento
{
    public static string Gerar(long clienteId, string tipoConteudo) =>
        $"{Prefixo(clienteId)}{Guid.NewGuid():N}{TiposArquivoMidia.Extensao(tipoConteudo)}";

    /// <summary>
    /// A chave é do cliente e tem o formato gerado aqui (sem subpastas nem
    /// <c>..</c>), com a extensão do tipo informado.
    /// </summary>
    public static bool Valida(string? chave, long clienteId, string tipoConteudo)
    {
        if (chave is null || !TiposArquivoMidia.Aceito(tipoConteudo))
        {
            return false;
        }

        var prefixo = Prefixo(clienteId);
        var extensao = TiposArquivoMidia.Extensao(tipoConteudo);

        if (!chave.StartsWith(prefixo, StringComparison.Ordinal) || !chave.EndsWith(extensao, StringComparison.Ordinal))
        {
            return false;
        }

        var nome = chave[prefixo.Length..^extensao.Length];
        return nome.Length == 32 && nome.All(char.IsAsciiHexDigitLower);
    }

    private static string Prefixo(long clienteId) => $"clientes/{clienteId}/midias/";
}

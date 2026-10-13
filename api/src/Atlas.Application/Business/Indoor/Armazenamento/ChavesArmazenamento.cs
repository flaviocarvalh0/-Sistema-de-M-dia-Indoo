namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>
/// Chave do objeto no bucket: <c>parceiros/{parceiroId}/midias/{guid}.{ext}</c>.
/// Gerada sempre pela API — o painel nunca escolhe onde grava — e conferida
/// quando volta, para um parceiro não registrar como sua a mídia de outro.
/// </summary>
public static class ChavesArmazenamento
{
    public static string Gerar(long parceiroId, string tipoConteudo) =>
        $"{Prefixo(parceiroId)}{Guid.NewGuid():N}{TiposArquivoMidia.Extensao(tipoConteudo)}";

    /// <summary>
    /// A chave é do parceiro e tem o formato gerado aqui (sem subpastas nem
    /// <c>..</c>), com a extensão do tipo informado.
    /// </summary>
    public static bool Valida(string? chave, long parceiroId, string tipoConteudo)
    {
        if (chave is null || !TiposArquivoMidia.Aceito(tipoConteudo))
        {
            return false;
        }

        var prefixo = Prefixo(parceiroId);
        var extensao = TiposArquivoMidia.Extensao(tipoConteudo);

        if (!chave.StartsWith(prefixo, StringComparison.Ordinal) || !chave.EndsWith(extensao, StringComparison.Ordinal))
        {
            return false;
        }

        var nome = chave[prefixo.Length..^extensao.Length];
        return nome.Length == 32 && nome.All(char.IsAsciiHexDigitLower);
    }

    private static string Prefixo(long parceiroId) => $"parceiros/{parceiroId}/midias/";
}

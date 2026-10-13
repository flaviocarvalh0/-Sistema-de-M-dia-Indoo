using System.Security.Cryptography;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>
/// Lê o arquivo enviado ao storage uma vez e devolve o SHA-256, o tamanho e o
/// tipo detectado pelos bytes. É o que a API grava na mídia — o hash
/// informado pelo painel só é aceito se bater, porque é com ele que a TV
/// confere o download (um hash errado faria a TV baixar o arquivo para sempre).
/// </summary>
public static class VerificadorArquivoMidia
{
    private const int TamanhoBuffer = 81920;

    /// <summary>Lê no máximo <see cref="Midia.TamanhoMaximoBytes"/> + 1 bytes: além disso o arquivo já é recusado.</summary>
    public static async Task<ArquivoVerificado> VerificarAsync(Stream conteudo, CancellationToken cancellationToken)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[TamanhoBuffer];
        var inicio = new byte[TiposArquivoMidia.BytesAssinatura];
        var bytesInicio = 0;
        long tamanho = 0;

        int lidos;
        while ((lidos = await conteudo.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bytesInicio < inicio.Length)
            {
                var copiar = Math.Min(lidos, inicio.Length - bytesInicio);
                Array.Copy(buffer, 0, inicio, bytesInicio, copiar);
                bytesInicio += copiar;
            }

            tamanho += lidos;
            if (tamanho > Midia.TamanhoMaximoBytes)
            {
                return new ArquivoVerificado(Hash: null, tamanho, TiposArquivoMidia.Detectar(inicio.AsSpan(0, bytesInicio)), ExcedeLimite: true);
            }

            sha256.AppendData(buffer, 0, lidos);
        }

        var hash = Convert.ToHexStringLower(sha256.GetHashAndReset());
        return new ArquivoVerificado(hash, tamanho, TiposArquivoMidia.Detectar(inicio.AsSpan(0, bytesInicio)), ExcedeLimite: false);
    }
}

/// <param name="Hash">SHA-256 em hexadecimal minúsculo; nulo quando o arquivo passou do limite.</param>
/// <param name="TamanhoBytes">Bytes lidos (no máximo o limite + 1 bloco).</param>
/// <param name="TipoDetectado">MIME pela assinatura; nulo se não é um formato aceito.</param>
/// <param name="ExcedeLimite">O arquivo passou de <see cref="Midia.TamanhoMaximoBytes"/>; a leitura parou ali.</param>
public sealed record ArquivoVerificado(string? Hash, long TamanhoBytes, string? TipoDetectado, bool ExcedeLimite);

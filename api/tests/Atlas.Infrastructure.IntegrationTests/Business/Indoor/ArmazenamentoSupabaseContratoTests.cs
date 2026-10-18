using System.Net.Http.Headers;
using System.Security.Cryptography;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Infrastructure.Business.Armazenamento;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Infrastructure.IntegrationTests.Business.Indoor;

/// <summary>
/// Contrato do <see cref="ArmazenamentoSupabase"/> contra uma Storage API de
/// verdade: o caminho completo que o painel e as TVs vão fazer (envio direto
/// do navegador pela URL assinada, conferência do hash pela API, download pela
/// URL de leitura). Só roda com as variáveis <c>INDOOR_SUPABASE_URL</c> e
/// <c>INDOOR_SUPABASE_CHAVE_SERVICO</c> — de preferência um projeto/bucket de
/// testes, porque cria e apaga objetos.
/// </summary>
public class ArmazenamentoSupabaseContratoTests
{
    private static readonly string? Url = Environment.GetEnvironmentVariable("INDOOR_SUPABASE_URL");
    private static readonly string? Chave = Environment.GetEnvironmentVariable("INDOOR_SUPABASE_CHAVE_SERVICO");
    private static readonly string Bucket = Environment.GetEnvironmentVariable("INDOOR_SUPABASE_BUCKET") ?? "midias-testes";

    [FactQueExigeSupabase]
    public async Task EnvioConferenciaLeituraERemocao_DevemFuncionarDePontaAPonta()
    {
        var armazenamento = new ArmazenamentoSupabase(
            new HttpClient(),
            new OpcoesSupabase { Url = new Uri(Url!.TrimEnd('/') + "/"), ChaveServico = Chave!, Bucket = Bucket },
            TimeProvider.System,
            NullLogger<ArmazenamentoSupabase>.Instance);

        await armazenamento.GarantirBucketAsync(CancellationToken.None);

        // Um PNG de verdade (assinatura) com conteúdo aleatório depois.
        var arquivo = new byte[64_000];
        Random.Shared.NextBytes(arquivo);
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(arquivo, 0);
        var chave = ChavesArmazenamento.Gerar(clienteId: 999_999, TiposArquivoMidia.Png);

        try
        {
            // 1. O painel pede a URL e envia direto ao storage, sem credencial.
            var envio = await armazenamento.PrepararEnvioAsync(chave, CancellationToken.None);
            using (var navegador = new HttpClient())
            {
                var corpo = new ByteArrayContent(arquivo);
                corpo.Headers.ContentType = new MediaTypeHeaderValue(TiposArquivoMidia.Png);
                var resposta = await navegador.PutAsync(envio.Url, corpo);
                resposta.IsSuccessStatusCode.Should().BeTrue($"o PUT na URL assinada respondeu {(int)resposta.StatusCode}");
            }

            // 2. A API confere o que chegou.
            var objeto = await armazenamento.ObterAsync(chave, CancellationToken.None);
            objeto.Should().NotBeNull();
            objeto!.TamanhoBytes.Should().Be(arquivo.Length);

            await using (var conteudo = await armazenamento.AbrirLeituraAsync(chave, CancellationToken.None))
            {
                var verificado = await VerificadorArquivoMidia.VerificarAsync(conteudo!, CancellationToken.None);
                verificado.Hash.Should().Be(Convert.ToHexStringLower(SHA256.HashData(arquivo)));
                verificado.TipoDetectado.Should().Be(TiposArquivoMidia.Png);
            }

            // 3. A TV baixa pela URL de leitura, sem credencial.
            var leitura = await armazenamento.GerarUrlLeituraAsync(chave, TimeSpan.FromMinutes(5), CancellationToken.None);
            using (var tv = new HttpClient())
            {
                (await tv.GetByteArrayAsync(leitura)).Should().Equal(arquivo);
            }
        }
        finally
        {
            await armazenamento.RemoverAsync(chave, CancellationToken.None);
        }

        (await armazenamento.ObterAsync(chave, CancellationToken.None)).Should().BeNull();
    }

    private sealed class FactQueExigeSupabaseAttribute : FactAttribute
    {
        public FactQueExigeSupabaseAttribute()
        {
            if (string.IsNullOrWhiteSpace(Url) || string.IsNullOrWhiteSpace(Chave))
            {
                Skip = "Defina INDOOR_SUPABASE_URL e INDOOR_SUPABASE_CHAVE_SERVICO para testar contra o Supabase.";
            }
        }
    }
}

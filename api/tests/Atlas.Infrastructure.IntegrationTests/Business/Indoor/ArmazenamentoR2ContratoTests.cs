using System.Net.Http.Headers;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Infrastructure.Business.Armazenamento;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Infrastructure.IntegrationTests.Business.Indoor;

/// <summary>
/// Contrato do <see cref="ArmazenamentoR2"/> contra o Cloudflare R2 de verdade
/// (ADR-025): envio pelo navegador na URL assinada, recusa de arquivo maior que
/// o declarado (a trava do teto), listagem, download e remoção. Só roda com
/// <c>INDOOR_R2_CONTA</c>, <c>INDOOR_R2_CHAVE</c> e <c>INDOOR_R2_SEGREDO</c>
/// — de preferência num bucket de testes (<c>INDOOR_R2_BUCKET</c>), porque cria
/// e apaga objetos.
/// </summary>
public class ArmazenamentoR2ContratoTests
{
    private static readonly string? Conta = Environment.GetEnvironmentVariable("INDOOR_R2_CONTA");
    private static readonly string? Chave = Environment.GetEnvironmentVariable("INDOOR_R2_CHAVE");
    private static readonly string? Segredo = Environment.GetEnvironmentVariable("INDOOR_R2_SEGREDO");
    private static readonly string Bucket = Environment.GetEnvironmentVariable("INDOOR_R2_BUCKET") ?? "midias-testes";

    [FactQueExigeR2]
    public async Task EnvioRecusaListagemLeituraERemocao_DevemFuncionarDePontaAPonta()
    {
        var armazenamento = new ArmazenamentoR2(
            new HttpClient(),
            new OpcoesR2 { Conta = Conta!, ChaveAcesso = Chave!, SegredoAcesso = Segredo!, Bucket = Bucket, Endereco = new Uri($"https://{Conta}.r2.cloudflarestorage.com") },
            TimeProvider.System,
            NullLogger<ArmazenamentoR2>.Instance);

        var arquivo = new byte[64_000];
        Random.Shared.NextBytes(arquivo);
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(arquivo, 0);
        var chave = ChavesArmazenamento.Gerar(clienteId: 999_999, TiposArquivoMidia.Png);
        var grande = ChavesArmazenamento.Gerar(clienteId: 999_999, TiposArquivoMidia.Png);

        try
        {
            // 1. Arquivo maior que o declarado: o R2 recusa (a URL assina o tamanho).
            var envioPequeno = await armazenamento.PrepararEnvioAsync(grande, TiposArquivoMidia.Png, 1000, CancellationToken.None);
            using (var navegador = new HttpClient())
            {
                var corpo = new ByteArrayContent(arquivo);
                corpo.Headers.ContentType = new MediaTypeHeaderValue(TiposArquivoMidia.Png);
                var resposta = await navegador.PutAsync(envioPequeno.Url, corpo);
                resposta.IsSuccessStatusCode.Should().BeFalse("o tamanho assinado era 1000 bytes");
            }

            (await armazenamento.ObterAsync(grande, CancellationToken.None)).Should().BeNull();

            // 2. Envio certo, direto do navegador, sem credencial.
            var envio = await armazenamento.PrepararEnvioAsync(chave, TiposArquivoMidia.Png, arquivo.Length, CancellationToken.None);
            using (var navegador = new HttpClient())
            {
                var corpo = new ByteArrayContent(arquivo);
                corpo.Headers.ContentType = new MediaTypeHeaderValue(TiposArquivoMidia.Png);
                var resposta = await navegador.PutAsync(envio.Url, corpo);
                resposta.IsSuccessStatusCode.Should().BeTrue($"o PUT na URL assinada respondeu {(int)resposta.StatusCode}");
            }

            // 3. A API confere e lista.
            (await armazenamento.ObterAsync(chave, CancellationToken.None))!.TamanhoBytes.Should().Be(arquivo.Length);
            var listados = new List<ObjetoListado>();
            await foreach (var o in armazenamento.ListarAsync(CancellationToken.None))
            {
                listados.Add(o);
            }

            listados.Should().Contain(o => o.Chave == chave && o.TamanhoBytes == arquivo.Length);

            // 4. A TV baixa pela URL de leitura.
            var leitura = await armazenamento.GerarUrlLeituraAsync(chave, TimeSpan.FromMinutes(5), CancellationToken.None);
            using (var tv = new HttpClient())
            {
                (await tv.GetByteArrayAsync(leitura)).Should().Equal(arquivo);
            }
        }
        finally
        {
            await armazenamento.RemoverAsync(chave, CancellationToken.None);
            await armazenamento.RemoverAsync(grande, CancellationToken.None);
        }

        (await armazenamento.ObterAsync(chave, CancellationToken.None)).Should().BeNull();
    }

    private sealed class FactQueExigeR2Attribute : FactAttribute
    {
        public FactQueExigeR2Attribute()
        {
            if (string.IsNullOrWhiteSpace(Conta) || string.IsNullOrWhiteSpace(Chave) || string.IsNullOrWhiteSpace(Segredo))
            {
                Skip = "Defina INDOOR_R2_CONTA, INDOOR_R2_CHAVE e INDOOR_R2_SEGREDO para testar contra o Cloudflare R2.";
            }
        }
    }
}

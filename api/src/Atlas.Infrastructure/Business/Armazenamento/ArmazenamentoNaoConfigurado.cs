using Atlas.Application.Business.Indoor.Armazenamento;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// Usado quando nem o R2 nem o Supabase estão configurados: a
/// API sobe (o painel, a Rede e as telas funcionam) e só o que mexe em
/// arquivo responde com erro claro.
/// </summary>
internal sealed class ArmazenamentoNaoConfigurado : IArmazenamentoMidias
{
    private static ArmazenamentoIndisponivelException Erro() =>
        new($"O storage de mídias não está configurado ({OpcoesR2.Secao} ou {OpcoesSupabase.Secao}).");

    public Task<EnvioPreparado> PrepararEnvioAsync(string chave, string tipoConteudo, long tamanhoBytes, CancellationToken cancellationToken) => throw Erro();
    public Task<Uri> GerarUrlLeituraAsync(string chave, TimeSpan validade, CancellationToken cancellationToken) => throw Erro();
    public Task<IReadOnlyDictionary<string, Uri>> GerarUrlsLeituraAsync(IReadOnlyCollection<string> chaves, TimeSpan validade, CancellationToken cancellationToken) => throw Erro();
    public Task<ObjetoArmazenado?> ObterAsync(string chave, CancellationToken cancellationToken) => throw Erro();
    public Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken) => throw Erro();
    public Task GravarAsync(string chave, byte[] conteudo, string tipoConteudo, CancellationToken cancellationToken) => throw Erro();
    public Task RemoverAsync(string chave, CancellationToken cancellationToken) => throw Erro();
}

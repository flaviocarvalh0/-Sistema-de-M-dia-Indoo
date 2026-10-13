using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Common.Models;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Parceiro do usuário logado, fixo no teste.</summary>
internal sealed class ParceiroAtualFake(long parceiroId) : IParceiroAtual
{
    public long ParceiroId { get; set; } = parceiroId;

    public Task<ParceiroDoUsuario> ObterAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ParceiroDoUsuario(ParceiroId, PapelParceiro.Proprietario, "America/Sao_Paulo"));
}

/// <summary>Storage em memória: o teste põe o arquivo como se o navegador tivesse enviado.</summary>
internal sealed class ArmazenamentoFake : IArmazenamentoMidias
{
    public Dictionary<string, byte[]> Arquivos { get; } = [];
    public List<string> Removidos { get; } = [];

    public Task<EnvioPreparado> PrepararEnvioAsync(string chave, CancellationToken cancellationToken) =>
        Task.FromResult(new EnvioPreparado(new Uri($"https://storage.test/envio/{chave}"), DateTimeOffset.UnixEpoch));

    public Task<Uri> GerarUrlLeituraAsync(string chave, TimeSpan validade, CancellationToken cancellationToken) =>
        Task.FromResult(new Uri($"https://storage.test/leitura/{chave}?s={(int)validade.TotalSeconds}"));

    public int ChamadasEmLote { get; private set; }

    public Task<IReadOnlyDictionary<string, Uri>> GerarUrlsLeituraAsync(IReadOnlyCollection<string> chaves, TimeSpan validade, CancellationToken cancellationToken)
    {
        ChamadasEmLote++;
        return Task.FromResult<IReadOnlyDictionary<string, Uri>>(chaves.ToDictionary(
            c => c,
            c => new Uri($"https://storage.test/leitura/{c}?s={(int)validade.TotalSeconds}")));
    }

    public Task<ObjetoArmazenado?> ObterAsync(string chave, CancellationToken cancellationToken) =>
        Task.FromResult(Arquivos.TryGetValue(chave, out var a) ? new ObjetoArmazenado(a.Length, null) : null);

    public Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Arquivos.TryGetValue(chave, out var a) ? new MemoryStream(a) : null);

    public Task RemoverAsync(string chave, CancellationToken cancellationToken)
    {
        Arquivos.Remove(chave);
        Removidos.Add(chave);
        return Task.CompletedTask;
    }
}

/// <summary>Só os nomes de auditoria (as consultas preenchem); o resto não é usado.</summary>
internal sealed class IdentityNomesFake : IIdentityService
{
    public Task<IReadOnlyDictionary<long, string>> GetUserNamesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<long, string>>(new Dictionary<long, string>());

    public Task<string?> GetUserNameAsync(long userId) => throw new NotImplementedException();
    public Task<bool> IsInRoleAsync(long userId, string role) => throw new NotImplementedException();
    public Task<bool> AuthorizeAsync(long userId, string policyName) => throw new NotImplementedException();
    public Task<UserDto?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyCollection<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result<long>> CreateUserAsync(string fullName, string userName, string? email, string temporaryPassword, bool mustChangePassword, IEnumerable<string> roles, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result> UpdateUserAsync(long userId, string fullName, string? email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result> ActivateUserAsync(long userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result> DeactivateUserAsync(long userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result> ResetPasswordAsync(long userId, string temporaryPassword, bool mustChangePassword, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result> AssignRoleAsync(long userId, string roleName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result> RemoveRoleAsync(long userId, string roleName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> HasPermissionAsync(long userId, string permission, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> IsSuperUserAsync(long userId) => throw new NotImplementedException();
}

internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = agora;

    public override DateTimeOffset GetUtcNow() => Agora;
}

internal static class Arquivos
{
    public static byte[] Mp4(int tamanho = 4_000) => ComInicio(tamanho, [0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m']);

    public static byte[] Png(int tamanho = 2_000) => ComInicio(tamanho, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    private static byte[] ComInicio(int tamanho, byte[] inicio)
    {
        var dados = new byte[tamanho];
        Random.Shared.NextBytes(dados);
        inicio.CopyTo(dados, 0);
        return dados;
    }
}

internal sealed class ConfiguracaoIndoorFake : IConfiguracaoIndoor
{
    public string UrlPainel => "https://painel.indoor.test/";
    public TimeSpan IntervaloSincronizacao => TimeSpan.FromMinutes(5);
    public TimeSpan ValidadeTokenDispositivo => TimeSpan.FromDays(30);
    public TimeSpan ValidadeUrlDownload => TimeSpan.FromHours(24);
}

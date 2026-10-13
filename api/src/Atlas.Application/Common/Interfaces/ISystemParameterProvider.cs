namespace Atlas.Application.Common.Interfaces;

public interface ISystemParameterProvider
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default);
    void InvalidateCache(string key);
}

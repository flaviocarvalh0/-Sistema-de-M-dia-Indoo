using Atlas.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Atlas.Infrastructure.Services
{
    public class SystemParameterProvider : ISystemParameterProvider
    {
        private readonly IApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15); // Cache dura 15 minutos

        public SystemParameterProvider(IApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"sys_param_{key.ToUpperInvariant()}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;

                var param = await _context.SystemParameters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Key == key, cancellationToken);

                return param?.Value;
            });
        }

        public async Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var rawValue = await GetValueAsync(key, cancellationToken);
            if (string.IsNullOrWhiteSpace(rawValue)) return default;

            try
            {
                // Se o tipo esperado for booleano, numérico ou primitivo direto
                if (typeof(T) == typeof(bool)) return (T)(object)bool.Parse(rawValue);
                if (typeof(T) == typeof(int)) return (T)(object)int.Parse(rawValue);
                if (typeof(T) == typeof(decimal)) return (T)(object)decimal.Parse(rawValue);

                // Caso contrário, assume que é uma estrutura complexa salva como JSON
                return JsonSerializer.Deserialize<T>(rawValue);
            }
            catch
            {
                return default;
            }
        }

        public void InvalidateCache(string key)
        {
            _cache.Remove($"sys_param_{key.ToUpperInvariant()}");
        }
    }
}

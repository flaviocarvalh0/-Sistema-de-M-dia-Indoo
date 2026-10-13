using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Platform.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Data.Initializers.Platform;

/// <summary>
/// Responsável por garantir que os ícones do sistema sejam populados no banco de dados.
/// Esta classe é utilizada durante o processo de inicialização da plataforma (seed).
/// </summary>
public class IconInitializer : IIconInitializer
{
    private readonly IApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<IconInitializer> _logger;

    public IconInitializer(IApplicationDbContext context, IWebHostEnvironment env, ILogger<IconInitializer> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Varre a pasta de seeds em busca de arquivos SVG e os insere no banco caso ainda não existam.
    /// </summary>
    /// <param name="adminId">ID do administrador para preenchimento de campos de auditoria.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task EnsureIconsAsync(long? adminId, CancellationToken cancellationToken = default)
    {
        // Define o caminho físico dos arquivos. 
        // Usamos AppContext.BaseDirectory para garantir que funcione no ambiente de execução (bin/Debug ou bin/Release).
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "Platform", "Icons");

        _logger.LogInformation("Verificando diretório de ícones: {Path}", seedPath);

        if (!Directory.Exists(seedPath))
        {
            _logger.LogError("Pasta de ícones não encontrada em: {Path}", seedPath);
            return;
        }

        var files = Directory.GetFiles(seedPath, "*.svg");
        _logger.LogInformation("Arquivos encontrados: {Count}", files.Length);

        if (files.Length == 0)
        {
            _logger.LogWarning("O diretório está vazio.");
            return;
        }

        int count = 0;
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            // Verifica se o ícone já existe pelo nome para evitar duplicatas em execuções subsequentes
            var exists = await _context.SystemIcons.AnyAsync(x => x.Name == fileName, cancellationToken);

            if (!exists)
            {
                var data = await File.ReadAllBytesAsync(file, cancellationToken);
                var now = DateTimeOffset.UtcNow;

                _context.SystemIcons.Add(new SystemIcon
                {
                    Name = fileName,
                    ContentType = "image/svg+xml",
                    Extension = ".svg",
                    SizeInBytes = data.Length,
                    Data = data,
                    IsSystemDefined = true,
                    Created = now,
                    LastModified = now,
                    CreatedBy = adminId,
                    LastModifiedBy = adminId
                });
                count++;
            }
        }

        if (count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Sucesso: {Count} novos ícones foram persistidos no banco de dados.", count);
        }
        else
        {
            _logger.LogInformation("Nenhum ícone novo para processar.");
        }
    }
}

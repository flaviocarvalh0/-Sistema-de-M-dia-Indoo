using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Constants;
using Atlas.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Data;

public static class InitialiserExtensions
{
    /// <summary>
    /// Aplica as Migrations, sincroniza os Metadados e provisiona a Infraestrutura de Histórico.
    /// Deve rodar em TODOS os ambientes (Development, Staging, Production).
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
    }

    /// <summary>
    /// Semeia usuários iniciais, roles e dados padrão.
    /// Tipicamente utilizado apenas em ambiente de Desenvolvimento.
    /// </summary>
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IEntityMetadataInitializer _metadataInitializer;
    private readonly IIconInitializer _iconInitializer;
    private readonly IEntityHistoryInitializer _historyInitializer;
    private readonly IConfiguration _configuration;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IEntityMetadataInitializer metadataInitializer,
        IIconInitializer iconInitializer,
        IEntityHistoryInitializer historyInitializer,
        IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _metadataInitializer = metadataInitializer;
        _iconInitializer = iconInitializer;
        _historyInitializer = historyInitializer;
        _configuration = configuration;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            _logger.LogInformation("Aplicando migrations do banco de dados...");
            await _context.Database.MigrateAsync();
            _logger.LogInformation("Migrations aplicadas com sucesso.");

            // Checa se a execução atual vem das ferramentas de design-time do EF Core (CLI/Studio)
            var isEfTools = string.Equals(
                Environment.GetEnvironmentVariable("AtlasEfTools"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (!isEfTools)
            {
                // 1. Sincroniza o catálogo em platform.entity_metadata
                _logger.LogInformation("Garantindo registros de metadados das entidades...");
                await _metadataInitializer.EnsureMetadataAsync(CancellationToken.None);

                // 2. Provisiona/Atualiza a infraestrutura de histórico no PostgreSQL
                _logger.LogInformation("Garantindo infraestrutura de histórico de entidades...");
                await _historyInitializer.EnsureHistoryInfrastructureAsync(CancellationToken.None);
            }
            else
            {
                _logger.LogInformation("Execução detectada via EF Tools (AtlasEfTools=true). Ignorando inicialização dinâmica de histórico.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocorreu um erro crítico ao aplicar as migrations e infraestrutura do banco.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ocorreu um erro ao executar os seeds do banco de dados.");

            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        await SeedIdentityDataAsync();

        _logger.LogInformation(
            "Iniciando carga de dados da plataforma...");

        var admin = await _userManager.FindByNameAsync("admin");
        var adminId = admin?.Id;

        await _iconInitializer.EnsureIconsAsync(
            adminId,
            CancellationToken.None);

        _logger.LogInformation(
            "Carga de dados da plataforma finalizada com sucesso.");
    }

    private async Task SeedIdentityDataAsync()
    {
        const string adminRoleName = Roles.Administrator;

        var adminRole =
            await _roleManager.FindByNameAsync(adminRoleName);

        if (adminRole is null)
        {
            adminRole = new ApplicationRole(adminRoleName)
            {
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var roleResult =
                await _roleManager.CreateAsync(adminRole);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Não foi possível criar o grupo " +
                    $"'{adminRoleName}': " +
                    FormatIdentityErrors(roleResult));
            }
        }

        const string adminUserName = "admin";

        var administrator =
            await _userManager.FindByNameAsync(adminUserName);

        if (administrator is not null)
        {
            return;
        }

        administrator = new ApplicationUser
        {
            UserName = adminUserName,
            Email = null,
            EmailConfirmed = true,
            FullName = "Administrador",
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsSuperUser = true
        };

        var initialPassword =
            _configuration["PlatformSettings:InitialAdminPassword"];

        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            throw new InvalidOperationException(
                "A configuração " +
                "'PlatformSettings:InitialAdminPassword' " +
                "não foi encontrada.");
        }

        var userResult = await _userManager.CreateAsync(
            administrator,
            initialPassword);

        if (!userResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Não foi possível criar o usuário administrador: " +
                FormatIdentityErrors(userResult));
        }

        administrator.CreatedBy = administrator.Id;
        administrator.UpdatedBy = administrator.Id;

        var updateUserResult =
            await _userManager.UpdateAsync(administrator);

        if (!updateUserResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Não foi possível atualizar a auditoria " +
                "do usuário administrador: " +
                FormatIdentityErrors(updateUserResult));
        }

        adminRole.CreatedBy = administrator.Id;
        adminRole.UpdatedBy = administrator.Id;

        var updateRoleResult =
            await _roleManager.UpdateAsync(adminRole);

        if (!updateRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Não foi possível atualizar a auditoria " +
                "do grupo de administradores: " +
                FormatIdentityErrors(updateRoleResult));
        }

        var addToRoleResult =
            await _userManager.AddToRoleAsync(
                administrator,
                adminRoleName);


        if (!addToRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Não foi possível associar o administrador " +
                "ao grupo de administradores: " +
                FormatIdentityErrors(addToRoleResult));
        }
    }
    private static string FormatIdentityErrors(
      IdentityResult result)
    {
        return string.Join(
            "; ",
            result.Errors.Select(error =>
                $"{error.Code}: {error.Description}"));
    }
}

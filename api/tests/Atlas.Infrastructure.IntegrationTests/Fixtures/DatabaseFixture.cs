using Atlas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Atlas.Infrastructure.IntegrationTests.Fixtures;
public class DatabaseFixture : IDisposable
{
    public IConfiguration Configuration { get; }
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<DatabaseFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Configuration.GetConnectionString(Atlas.Shared.Services.Database);

        ConnectionString = connectionString
            ?? throw new InvalidOperationException(
                $"A ConnectionString '{Atlas.Shared.Services.Database}' não foi encontrada nos User Secrets ou Variáveis de Ambiente do projeto de testes.");
    }

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }

    public void Dispose()
    {
    }
}

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}

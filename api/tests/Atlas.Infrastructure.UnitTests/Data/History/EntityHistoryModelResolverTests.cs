using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.Data.History.Modeling;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Infrastructure.UnitTests;

public class EntityHistoryModelResolverTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            // Não abre conexão com o banco real. 
            // Serve apenas para o EF construir a árvore de metadados física do PostgreSQL.
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=atlas_history_tests;" +
                "Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Resolve_DeveResolverSystemParameter()
    {
        using var context = CreateContext();
        var resolver = new EntityHistoryModelResolver(context);

        var metadata = new EntityMetadata
        {
            Id = 1,
            TableName = "system_parameter",
            DisplayLabel = "Parâmetros",
            RouteKey = "system-parameter",
            EnableHistory = true
        };

        var definition = resolver.Resolve(metadata);

        definition.ClrType.Should().Be(typeof(SystemParameter));
        definition.SourceSchema.Should().Be("platform");
        definition.SourceTable.Should().Be("system_parameter");
        definition.HistorySchema.Should().Be("history");
        definition.HistoryTable.Should().Be("platform_system_parameter_history");
        definition.PrimaryKeyColumn.Should().Be("id");
        definition.PrimaryKeyStoreType.Should().Be("bigint");
    }

    [Fact]
    public void Resolve_DeveMapearColunasPersistidasDeSystemParameter()
    {
        using var context = CreateContext();
        var resolver = new EntityHistoryModelResolver(context);

        var metadata = new EntityMetadata
        {
            Id = 1,
            TableName = "system_parameter",
            DisplayLabel = "Parâmetros",
            RouteKey = "system-parameter",
            EnableHistory = true
        };

        var definition = resolver.Resolve(metadata);

        var columnNames = definition.Columns
            .Select(column => column.ColumnName)
            .ToArray();

        columnNames.Should().Contain(new[]
        {
            "id", "key", "value", "description", "group",
            "data_type", "created", "created_by", "last_modified", "last_modified_by"
        });
    }

    [Fact]
    public void Resolve_DevePreservarTiposRelacionaisDeSystemParameter()
    {
        using var context = CreateContext();
        var resolver = new EntityHistoryModelResolver(context);

        var metadata = new EntityMetadata
        {
            Id = 1,
            TableName = "system_parameter",
            DisplayLabel = "Parâmetros",
            RouteKey = "system-parameter",
            EnableHistory = true
        };

        var definition = resolver.Resolve(metadata);

        definition.Columns
            .Single(column => column.ColumnName == "value")
            .StoreType.Should().Be("text");

        definition.Columns
            .Single(column => column.ColumnName == "key")
            .StoreType.Should().Contain("character varying");
    }

    [Fact]
    public void Resolve_NaoDeveIncluirXminNoHistorico()
    {
        using var context = CreateContext();
        var resolver = new EntityHistoryModelResolver(context);

        var metadata = new EntityMetadata
        {
            Id = 1,
            TableName = "system_parameter",
            DisplayLabel = "Parâmetros",
            RouteKey = "system-parameter",
            EnableHistory = true
        };

        var definition = resolver.Resolve(metadata);

        definition.Columns.Should()
            .NotContain(column => string.Equals(column.ColumnName, "xmin", StringComparison.OrdinalIgnoreCase));

        definition.Columns.Should()
            .NotContain(column => string.Equals(column.PropertyName, "Version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_DeveRejeitarTabelaNaoMapeada()
    {
        using var context = CreateContext();
        var resolver = new EntityHistoryModelResolver(context);

        var metadata = new EntityMetadata
        {
            TableName = "tabela_inexistente",
            DisplayLabel = "Tabela inexistente",
            RouteKey = "tabela-inexistente",
            EnableHistory = true
        };

        var action = () => resolver.Resolve(metadata);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nenhuma entidade do EF Core*");
    }

    [Fact]
    public void Resolve_DeveRejeitarEntityMetadata()
    {
        using var context = CreateContext();
        var resolver = new EntityHistoryModelResolver(context);

        var metadata = new EntityMetadata
        {
            TableName = "entity_metadata",
            DisplayLabel = "Metadados",
            RouteKey = "entity-metadata",
            EnableHistory = true
        };

        var action = () => resolver.Resolve(metadata);

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*EntityMetadata*");
    }
}

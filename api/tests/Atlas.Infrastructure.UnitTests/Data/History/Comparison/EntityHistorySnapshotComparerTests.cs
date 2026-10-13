using Atlas.Infrastructure.Data.History.Comparison;
using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Reading;
using FluentAssertions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Data.History.Comparison;
public class EntityHistorySnapshotComparerTests
{
    private readonly EntityHistorySnapshotComparer _comparer = new();

    #region Testes Essenciais de Comparação

    [Fact]
    public void Compare_DeveIdentificarCampoAlterado()
    {
        // Arrange
        var definition = CreateDefinition();

        var previous = CreateSnapshot(
            historyId: 1,
            values: new Dictionary<string, object?>
            {
                ["key"] = "Test.Key",
                ["value"] = "initial",
                ["description"] = "Description"
            });

        var current = CreateSnapshot(
            historyId: 2,
            values: new Dictionary<string, object?>
            {
                ["key"] = "Test.Key",
                ["value"] = "updated",
                ["description"] = "Description"
            });

        var batch = new EntityHistorySnapshotBatch
        {
            Snapshots = [current, previous],
            ComparisonBaseline = null,
            HasMore = false,
            NextCursor = null
        };

        // Act
        var result = _comparer.Compare(definition, batch);

        // Assert
        var currentComparison = result.First();

        currentComparison.Changes.Should().ContainSingle();

        var change = currentComparison.Changes.Single();
        change.ColumnName.Should().Be("value");
        change.PreviousValue.Should().Be("initial");
        change.CurrentValue.Should().Be("updated");
    }

    [Fact]
    public void Compare_DeveRepresentarValoresDaPrimeiraVersao()
    {
        // Arrange
        var definition = CreateDefinition();

        var initial = CreateSnapshot(
            historyId: 1,
            values: new Dictionary<string, object?>
            {
                ["key"] = "Test.Key",
                ["value"] = "initial",
                ["description"] = null
            });

        var batch = new EntityHistorySnapshotBatch
        {
            Snapshots = [initial],
            ComparisonBaseline = null,
            HasMore = false,
            NextCursor = null
        };

        // Act
        var result = _comparer.Compare(definition, batch);

        // Assert
        var version = result.Single();

        version.IsInitialVersion.Should().BeTrue();

        version.Changes.Should()
            .Contain(change =>
                change.ColumnName == "key" &&
                change.PreviousValue == null &&
                Equals(change.CurrentValue, "Test.Key"));

        version.Changes.Should()
            .NotContain(change => change.ColumnName == "description");
    }

    [Fact]
    public void Compare_DeveUsarBaselineNaUltimaVersaoDaPagina()
    {
        // Arrange
        var definition = CreateDefinition();

        var version3 = CreateSnapshot(3, new Dictionary<string, object?> { ["value"] = "C" });
        var version2 = CreateSnapshot(2, new Dictionary<string, object?> { ["value"] = "B" });
        var version1 = CreateSnapshot(1, new Dictionary<string, object?> { ["value"] = "A" });

        var batch = new EntityHistorySnapshotBatch
        {
            Snapshots = [version3, version2],
            ComparisonBaseline = version1,
            HasMore = true,
            NextCursor = 2
        };

        // Act
        var result = _comparer.Compare(definition, batch);

        // Assert
        var version2Comparison = result.Single(item => item.HistoryId == 2);

        version2Comparison.IsInitialVersion.Should().BeFalse();

        version2Comparison.Changes.Should()
            .ContainSingle(change =>
                Equals(change.PreviousValue, "A") &&
                Equals(change.CurrentValue, "B"));
    }

    [Fact]
    public void Compare_NaoDeveExibirCamposDeAuditoriaComoAlteracoes()
    {
        // Arrange
        var definition = CreateAuditableDefinition();

        var previous = CreateSnapshot(
            1,
            new Dictionary<string, object?>
            {
                ["value"] = "A",
                ["last_modified"] = DateTimeOffset.UtcNow.AddMinutes(-1),
                ["last_modified_by"] = "user-1"
            });

        var current = CreateSnapshot(
            2,
            new Dictionary<string, object?>
            {
                ["value"] = "B",
                ["last_modified"] = DateTimeOffset.UtcNow,
                ["last_modified_by"] = "user-2"
            });

        var batch = new EntityHistorySnapshotBatch
        {
            Snapshots = [current, previous],
            ComparisonBaseline = null,
            HasMore = false,
            NextCursor = null
        };

        // Act
        var result = _comparer.Compare(definition, batch);

        // Assert
        result.First().Changes.Should()
            .ContainSingle(change => change.ColumnName == "value");
    }

    [Theory]
    [InlineData(1, 1L)]
    [InlineData(1, 1.0)]
    [InlineData(100, (short)100)]
    public void Compare_NaoDeveConsiderarNumerosEquivalentesComoAlteracao(
        object previousValue,
        object currentValue)
    {
        // Arrange
        var definition = CreateDefinition();

        var previous = CreateSnapshot(1, new Dictionary<string, object?> { ["value"] = previousValue });
        var current = CreateSnapshot(2, new Dictionary<string, object?> { ["value"] = currentValue });

        var batch = new EntityHistorySnapshotBatch
        {
            Snapshots = [current, previous],
            ComparisonBaseline = null,
            HasMore = false,
            NextCursor = null
        };

        // Act
        var result = _comparer.Compare(definition, batch);

        // Assert
        result.First().Changes.Should().BeEmpty("pois tipos numéricos diferentes com valores equivalentes não são alterações reais");
    }

    [Fact]
    public void Compare_NaoDeveConsiderarDatasComKindsDiferentesComoAlteracao_QuandoDataTimesIguais()
    {
        // Arrange
        var definition = CreateDefinition();
        var now = DateTime.UtcNow;

        var previous = CreateSnapshot(1, new Dictionary<string, object?> { ["value"] = now });
        var current = CreateSnapshot(2, new Dictionary<string, object?> { ["value"] = DateTime.SpecifyKind(now, DateTimeKind.Unspecified) });

        var batch = new EntityHistorySnapshotBatch
        {
            Snapshots = [current, previous],
            ComparisonBaseline = null,
            HasMore = false,
            NextCursor = null
        };

        // Act
        var result = _comparer.Compare(definition, batch);

        // Assert
        result.First().Changes.Should().BeEmpty();
    }

    #endregion

    #region Builders e Helpers

    private static EntityHistoryDefinition CreateDefinition()
    {
        return new EntityHistoryDefinition
        {
            ClrType = typeof(object),
            SourceSchema = "platform",
            SourceTable = "system_parameter",
            HistorySchema = "history",
            HistoryTable = "platform_system_parameter_history",
            PrimaryKeyColumn = "id",
            PrimaryKeyStoreType = "bigint",
            Columns = new[]
            {
                CreateColumn("Key", "key"),
                CreateColumn("Value", "value"),
                CreateColumn("Description", "description")
            }
        };
    }

    private static EntityHistoryDefinition CreateAuditableDefinition()
    {
        return new EntityHistoryDefinition
        {
            ClrType = typeof(object),
            SourceSchema = "platform",
            SourceTable = "system_parameter",
            HistorySchema = "history",
            HistoryTable = "platform_system_parameter_history",
            PrimaryKeyColumn = "id",
            PrimaryKeyStoreType = "bigint",
            Columns = new[]
            {
                CreateColumn("Value", "value"),
                // Para colunas de auditoria/controle de sistema que devem ser ignoradas no diff
                CreateColumn("LastModified", "last_modified"),
                CreateColumn("LastModifiedBy", "last_modified_by")
            }
        };
    }

    // Helper para preencher todas as propriedades 'required' da EntityHistoryColumnDefinition
    private static EntityHistoryColumnDefinition CreateColumn(
        string propertyName,
        string columnName,
        string storeType = "text")
    {
        return new EntityHistoryColumnDefinition
        {
            PropertyName = propertyName,
            ColumnName = columnName,
            StoreType = storeType,
            IsNullable = true,
            IsPrimaryKey = false,
            IsShadowProperty = false,
            ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never
        };
    }

    private static EntityHistorySnapshot CreateSnapshot(
        long historyId,
        IReadOnlyDictionary<string, object?> values)
    {
        return new EntityHistorySnapshot
        {
            HistoryId = historyId,
            RecordId = 100,
            HistoryCreatedAt = DateTimeOffset.UtcNow,
            Values = values
        };
    }

    #endregion
}

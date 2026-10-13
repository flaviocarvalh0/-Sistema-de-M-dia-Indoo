using System.ComponentModel.DataAnnotations;
using Atlas.Application.Common.Interfaces;
using Atlas.Infrastructure.Data.History;
using Atlas.Infrastructure.Data.History.Comparison;
using Atlas.Infrastructure.Data.History.Reading;
using Moq;

namespace Atlas.Infrastructure.IntegrationTests.Data.History;
public class EntityHistoryQueryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEntityHistoryModelResolver> _modelResolverMock;
    private readonly Mock<IEntityHistorySnapshotReader> _snapshotReaderMock;
    private readonly Mock<IEntityHistorySnapshotComparer> _snapshotComparerMock;
    private readonly Mock<IIdentityService> _identityServiceMock;
    private readonly EntityHistoryQueryService _service;

    public EntityHistoryQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        _modelResolverMock = new Mock<IEntityHistoryModelResolver>();
        _snapshotReaderMock = new Mock<IEntityHistorySnapshotReader>();
        _snapshotComparerMock = new Mock<IEntityHistorySnapshotComparer>();
        _identityServiceMock = new Mock<IIdentityService>();

        _service = new EntityHistoryQueryService(
            _context,
            _modelResolverMock.Object,
            _snapshotReaderMock.Object,
            _snapshotComparerMock.Object,
            _identityServiceMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region 1. Validação de RouteKey Inexistente

    [Fact]
    public async Task GetAsync_DeveRejeitarRouteKeyInexistente()
    {
        // Arrange
        const string invalidRouteKey = "entidade-inexistente";

        // Act
        var action = () => _service.GetAsync(invalidRouteKey, recordId: 1);

        // Assert
        await action.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{invalidRouteKey}*");
    }

    #endregion

    #region 2. Retorno de Versões e Resolução de Nomes/Campos

    [Fact]
    public async Task GetAsync_DeveRetornarVersoesComAlteracoesENomesDeUsuariosResolvidos()
    {
        // Arrange
        const string routeKey = "parametros-sistema";
        const long recordId = 10;
        const long userId = 42;

        await SeedMetadataAsync(routeKey, displayLabel: "Parâmetros", enableHistory: true);

        var definition = CreateDummyDefinition();
        _modelResolverMock
            .Setup(r => r.Resolve(It.IsAny<EntityMetadata>()))
            .Returns(definition);

        var snapshotBatch = CreateEmptySnapshotBatch();

        _snapshotReaderMock
            .Setup(r => r.ReadAsync(definition, recordId, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshotBatch);

        var comparisons = new[]
        {
            new EntityHistoryVersionComparison
            {
                HistoryId = 100,
                RecordId = recordId,
                HistoryCreatedAt = DateTimeOffset.UtcNow,
                IsInitialVersion = false,
                Values = new Dictionary<string, object?>
                {
                    { "LastModifiedBy", userId.ToString() },
                    { "LastModified", DateTimeOffset.UtcNow }
                },
                Changes = new[]
                {
                    new EntityHistoryFieldChange
                    {
                        PropertyName = "Value",
                        ColumnName = "Value",
                        PreviousValue = "OldValue",
                        CurrentValue = "NewValue"
                    }
                }
            }
        };

        _snapshotComparerMock
            .Setup(c => c.Compare(definition, snapshotBatch))
            .Returns(comparisons);

        _identityServiceMock
            .Setup(i => i.GetUserNamesAsync(It.Is<IEnumerable<long>>(ids => ids.Contains(userId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, string> { { userId, "João da Silva" } });

        // Act
        var result = await _service.GetAsync(routeKey, recordId);

        // Assert
        result.Should().NotBeNull();
        result.RecordId.Should().Be(recordId);
        result.EntityLabel.Should().Be("Parâmetros");
        result.HistoryEnabled.Should().BeTrue();
        result.Versions.Should().HaveCount(1);

        var version = result.Versions.First();
        version.ChangedById.Should().Be(userId.ToString());
        version.ChangedByName.Should().Be("João da Silva");
        version.Changes.Should().Contain(change => change.PropertyName == "Value" && (string?)change.CurrentValue == "NewValue");
    }

    #endregion

    #region 3. Histórico Desabilitado com Versões Existentes

    [Fact]
    public async Task GetAsync_DeveRetornarVersoesMesmoQuandoHistoricoEstiverDesabilitadoNosMetadados()
    {
        // Arrange
        const string routeKey = "entidade-desabilitada";
        const long recordId = 5;

        await SeedMetadataAsync(routeKey, displayLabel: "Entidade Inativa", enableHistory: false);

        var definition = CreateDummyDefinition();
        _modelResolverMock.Setup(r => r.Resolve(It.IsAny<EntityMetadata>())).Returns(definition);

        var emptyBatch = CreateEmptySnapshotBatch();

        _snapshotReaderMock
            .Setup(r => r.ReadAsync(definition, recordId, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyBatch);

        _snapshotComparerMock
            .Setup(c => c.Compare(definition, It.IsAny<EntityHistorySnapshotBatch>()))
            .Returns(new[]
            {
                new EntityHistoryVersionComparison
                {
                    HistoryId = 1,
                    RecordId = recordId,
                    HistoryCreatedAt = DateTimeOffset.UtcNow,
                    IsInitialVersion = true,
                    Values = new Dictionary<string, object?>(),
                    Changes = Array.Empty<EntityHistoryFieldChange>()
                }
            });

        _identityServiceMock
            .Setup(i => i.GetUserNamesAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, string>());

        // Act
        var result = await _service.GetAsync(routeKey, recordId);

        // Assert
        result.HistoryEnabled.Should().BeFalse();
        result.Versions.Should().NotBeEmpty();
    }

    #endregion

    #region 4. Preservação de Paginação

    [Fact]
    public async Task GetAsync_DevePreservarInformacoesDePaginacao()
    {
        // Arrange
        const string routeKey = "entidade-paginada";
        const long recordId = 1;
        const long nextCursor = 50;

        await SeedMetadataAsync(routeKey, "Paginada", enableHistory: true);

        var definition = CreateDummyDefinition();
        _modelResolverMock.Setup(r => r.Resolve(It.IsAny<EntityMetadata>())).Returns(definition);

        var batchWithMore = new EntityHistorySnapshotBatch
        {
            Snapshots = Array.Empty<EntityHistorySnapshot>(),
            HasMore = true,
            NextCursor = nextCursor
        };

        _snapshotReaderMock
            .Setup(r => r.ReadAsync(definition, recordId, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchWithMore);

        _snapshotComparerMock
            .Setup(c => c.Compare(definition, batchWithMore))
            .Returns(Array.Empty<EntityHistoryVersionComparison>());

        // Act
        var result = await _service.GetAsync(routeKey, recordId, pageSize: 10);

        // Assert
        result.HasMore.Should().BeTrue();
        result.NextCursor.Should().Be(nextCursor);
    }

    #endregion

    #region 5. Formatação de Tipos Especiais (Booleanos, Enums e Datas)

    [Fact]
    public async Task GetAsync_DeveFormatarValoresBooleanosEDatasCorretamente()
    {
        // Arrange
        const string routeKey = "entidade-com-booleano";
        const long recordId = 99;

        await SeedMetadataAsync(routeKey, "Entidade Boolean", enableHistory: true);

        var columns = new[]
        {
            CreateColumn(nameof(SampleModel.IsActive), "IsActive"),
            CreateColumn(nameof(SampleModel.Status), "Status")
        };

        var definition = CreateDummyDefinition(typeof(SampleModel), columns);

        _modelResolverMock.Setup(r => r.Resolve(It.IsAny<EntityMetadata>())).Returns(definition);

        _snapshotReaderMock
            .Setup(r => r.ReadAsync(definition, recordId, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptySnapshotBatch());

        _snapshotComparerMock
            .Setup(c => c.Compare(definition, It.IsAny<EntityHistorySnapshotBatch>()))
            .Returns(new[]
            {
                new EntityHistoryVersionComparison
                {
                    HistoryId = 1,
                    RecordId = recordId,
                    HistoryCreatedAt = DateTimeOffset.UtcNow,
                    IsInitialVersion = false,
                    Values = new Dictionary<string, object?>(),
                    Changes = new[]
                    {
                        new EntityHistoryFieldChange
                        {
                            PropertyName = nameof(SampleModel.IsActive),
                            ColumnName = "IsActive",
                            PreviousValue = false,
                            CurrentValue = true
                        },
                        new EntityHistoryFieldChange
                        {
                            PropertyName = nameof(SampleModel.Status),
                            ColumnName = "Status",
                            PreviousValue = SampleStatus.Inactive,
                            CurrentValue = SampleStatus.Active
                        }
                    }
                }
            });

        _identityServiceMock
            .Setup(i => i.GetUserNamesAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, string>());

        // Act
        var result = await _service.GetAsync(routeKey, recordId);

        // Assert
        var version = result.Versions.Single();

        var boolChange = version.Changes.Single(c => c.PropertyName == nameof(SampleModel.IsActive));
        boolChange.PreviousDisplayValue.Should().Be("Não");
        boolChange.CurrentDisplayValue.Should().Be("Sim");

        var enumChange = version.Changes.Single(c => c.PropertyName == nameof(SampleModel.Status));
        enumChange.DisplayLabel.Should().Be("Status da Entidade");
        enumChange.CurrentDisplayValue.Should().Be("Ativo");
    }

    #endregion

    #region Helpers & Test Classes

    private async Task SeedMetadataAsync(string routeKey, string displayLabel, bool enableHistory)
    {
        _context.EntityMetadata.Add(new EntityMetadata
        {
            RouteKey = routeKey,
            DisplayLabel = displayLabel,
            EnableHistory = enableHistory
        });

        await _context.SaveChangesAsync();
    }

    private static EntityHistorySnapshotBatch CreateEmptySnapshotBatch()
    {
        return new EntityHistorySnapshotBatch
        {
            Snapshots = Array.Empty<EntityHistorySnapshot>(),
            HasMore = false,
            NextCursor = null
        };
    }

    private static EntityHistoryDefinition CreateDummyDefinition(
        Type? clrType = null,
        IReadOnlyCollection<EntityHistoryColumnDefinition>? columns = null)
    {
        var defaultColumns = new[]
        {
            CreateColumn("Value", "Value"),
            CreateColumn("LastModifiedBy", "LastModifiedBy"),
            CreateColumn("LastModified", "LastModified")
        };

        return new EntityHistoryDefinition
        {
            SourceSchema = "dbo",
            SourceTable = "SampleTable",
            HistorySchema = "history",
            HistoryTable = "SampleTable_History",
            PrimaryKeyColumn = "Id",
            PrimaryKeyStoreType = "bigint",
            ClrType = clrType ?? typeof(SampleModel),
            Columns = columns ?? defaultColumns
        };
    }

    private static EntityHistoryColumnDefinition CreateColumn(
        string propertyName,
        string columnName,
        string storeType = "varchar(100)",
        bool isNullable = true,
        bool isPrimaryKey = false)
    {
        return new EntityHistoryColumnDefinition
        {
            PropertyName = propertyName,
            ColumnName = columnName,
            StoreType = storeType,
            IsNullable = isNullable,
            IsPrimaryKey = isPrimaryKey,
            IsShadowProperty = false,
            ValueGenerated = ValueGenerated.Never
        };
    }

    private enum SampleStatus
    {
        [Display(Name = "Inativo")]
        Inactive,

        [Display(Name = "Ativo")]
        Active
    }

    private class SampleModel
    {
        [Display(Name = "Ativo?")]
        public bool IsActive { get; set; }

        [Display(Name = "Status da Entidade")]
        public SampleStatus Status { get; set; }

        public string Value { get; set; } = string.Empty;
        public string? LastModifiedBy { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }

    #endregion
}

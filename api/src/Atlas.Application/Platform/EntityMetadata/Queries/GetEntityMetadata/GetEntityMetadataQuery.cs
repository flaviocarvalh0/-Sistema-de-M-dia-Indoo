using Dapper;

namespace Atlas.Application.Platform.EntityMetadata.Queries.GetEntityMetadata;

public record GetEntityMetadataQuery : IRequest<Result<List<EntityMetadataDto>>>;

public class GetEntityMetadataQueryHandler : IRequestHandler<GetEntityMetadataQuery, Result<List<EntityMetadataDto>>>
{
    private readonly IDapperContext _dapperContext;

    public GetEntityMetadataQueryHandler(IDapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    public async Task<Result<List<EntityMetadataDto>>> Handle(GetEntityMetadataQuery request, CancellationToken ct)
    {
        const string sql = @"
            SELECT 
                em.id                 AS ""Id"",
                em.table_name         AS ""TableName"",
                em.display_label      AS ""DisplayLabel"",
                em.route_key          AS ""RouteKey"",
                em.enable_history     AS ""EnableHistory"",
                em.enable_export      AS ""EnableExport"",
                em.default_page_size  AS ""DefaultPageSize"",
                em.code_prefix        AS ""CodePrefix"",
                em.xmin               AS ""Version""
            FROM platform.entity_metadata em
            ORDER BY em.display_label ASC;";

        using var connection = _dapperContext.CreateConnection();
        var result = await connection.QueryAsync<EntityMetadataDto>(sql);

        return Result<List<EntityMetadataDto>>.Success(result.ToList());
    }
}

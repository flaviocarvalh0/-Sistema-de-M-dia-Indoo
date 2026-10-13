using Atlas.Application.Common.Models.Pagination;
using Dapper;

namespace Atlas.Application.Platform.SystemParameters.Queries.GetSystemParameters;

[Authorize(Policy = PermissionKeys.Parameters.View)]
public record GetSystemParametersQuery : IRequest<Result<PaginatedList<SystemParameterDto>>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class GetSystemParametersQueryHandler(IDapperContext dapperContext)
    : IRequestHandler<GetSystemParametersQuery, Result<PaginatedList<SystemParameterDto>>>
{
    public async Task<Result<PaginatedList<SystemParameterDto>>> Handle(
        GetSystemParametersQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dapperContext.CreateConnection();

        const string metadataSql = @"
            SELECT default_page_size 
            FROM platform.entity_metadata 
            WHERE table_name = @TableName;
        ";

        var defaultPageSize = await connection.QueryFirstOrDefaultAsync<int?>(metadataSql, new { TableName = "system_parameter" }) ?? 100;

        var pageSize = request.PaginationParams.PageSize > 0
            ? request.PaginationParams.PageSize
            : defaultPageSize;

        var pageNumber = request.PaginationParams.PageNumber > 0
            ? request.PaginationParams.PageNumber
            : 1;

        var offset = (pageNumber - 1) * pageSize;

        var searchCondition = string.IsNullOrWhiteSpace(request.SearchTerm)
            ? string.Empty
            : "WHERE (sp.key ILIKE @SearchTerm OR sp.description ILIKE @SearchTerm OR sp.group ILIKE @SearchTerm)";

        var searchTermParam = string.IsNullOrWhiteSpace(request.SearchTerm)
            ? null
            : $"%{request.SearchTerm}%";

        var countSql = $@"
            SELECT COUNT(1) 
            FROM platform.system_parameter sp
            {searchCondition};
        ";

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { SearchTerm = searchTermParam });

        var dataSql = $@"
            SELECT 
                sp.id                     AS ""Id"",
                sp.key                    AS ""Key"",
                sp.value                  AS ""Value"",
                sp.description            AS ""Description"",
                sp.group                  AS ""Group"",
                sp.data_type              AS ""DataType"",
                sp.created                AS ""Created"",
                sp.created_by             AS ""CreatedBy"",
                uc.full_name              AS ""CreatedByName"",
                sp.last_modified          AS ""LastModified"",
                sp.last_modified_by       AS ""LastModifiedBy"",
                um.full_name              AS ""LastModifiedByName"",
                sp.xmin                   AS ""Version""
            FROM 
                platform.system_parameter sp
                INNER JOIN platform.user uc ON sp.created_by = uc.id
                INNER JOIN platform.user um ON sp.last_modified_by = um.id
            {searchCondition}
            ORDER BY 
                sp.group, 
                sp.key
            LIMIT @PageSize OFFSET @Offset;
        ";

        var items = await connection.QueryAsync<SystemParameterDto>(dataSql, new
        {
            SearchTerm = searchTermParam,
            PageSize = pageSize,
            Offset = offset
        });

        var paginatedList = new PaginatedList<SystemParameterDto>(items.ToList(), totalCount, pageNumber, pageSize);

        return Result<PaginatedList<SystemParameterDto>>.Success(paginatedList);
    }
}

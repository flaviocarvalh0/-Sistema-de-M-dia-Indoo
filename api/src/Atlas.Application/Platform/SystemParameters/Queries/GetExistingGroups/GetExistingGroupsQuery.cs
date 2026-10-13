using Dapper;

namespace Atlas.Application.Platform.SystemParameters.Queries.GetExistingGroups;
public record GetExistingGroupsQuery : IRequest<Result<IEnumerable<string>>>;

public class GetExistingGroupsQueryHandler(IDapperContext dapperContext)
    : IRequestHandler<GetExistingGroupsQuery, Result<IEnumerable<string>>>
{
    public async Task<Result<IEnumerable<string>>> Handle(GetExistingGroupsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT DISTINCT ""group"" 
            FROM platform.system_parameter 
            ORDER BY ""group"";";

        using var connection = dapperContext.CreateConnection();
        var groups = await connection.QueryAsync<string>(sql);

        return Result<IEnumerable<string>>.Success(groups);
    }
}

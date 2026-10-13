namespace Atlas.Application.Platform.SystemModules.Queries.GetAvailableEntities;

public record AvailableEntityDto(long Id, string TableName);

public record GetAvailableEntitiesQuery : IRequest<Result<List<AvailableEntityDto>>>;

public class GetAvailableEntitiesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAvailableEntitiesQuery, Result<List<AvailableEntityDto>>>
{
    public async Task<Result<List<AvailableEntityDto>>> Handle(
        GetAvailableEntitiesQuery request, CancellationToken cancellationToken)
    {
        // Busca entidades que não possuem módulo vinculado
        var available = await context.EntityMetadata
            .AsNoTracking()
            .Where(x => x.SystemModuleId == null)
            .OrderBy(x => x.TableName)
            .Select(x => new AvailableEntityDto(x.Id, x.TableName))
            .ToListAsync(cancellationToken);

        return Result<List<AvailableEntityDto>>.Success(available);
    }
}
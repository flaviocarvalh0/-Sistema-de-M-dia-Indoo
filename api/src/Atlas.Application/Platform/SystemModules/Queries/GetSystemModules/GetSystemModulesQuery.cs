using Atlas.Application.Platform.SystemModules.Dtos;

namespace Atlas.Application.Platform.SystemModules.Queries.GetApplications;

public record GetSystemModulesQuery : IRequest<Result<List<SystemModuleDto>>>;

public class GetSystemModulesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSystemModulesQuery, Result<List<SystemModuleDto>>>
{
    public async Task<Result<List<SystemModuleDto>>> Handle(GetSystemModulesQuery request, CancellationToken cancellationToken)
    {
        var applications = await context.SystemModules
            .AsNoTracking()
            .Include(x => x.Entities)
            .OrderBy(x => x.SortOrder)
            .Select(x => new SystemModuleDto(
                x.Id,
                x.Name,
                x.Description,
                x.RoutePrefix,
                x.SortOrder,
                x.SystemIconId,
                x.HexColor,
                x.Entities.Select(e => e.TableName).ToList(),
                EF.Property<uint>(x, "Version")
            ))
            .ToListAsync(cancellationToken);

        return Result<List<SystemModuleDto>>.Success(applications);
    }
}

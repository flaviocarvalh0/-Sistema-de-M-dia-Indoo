namespace Atlas.Application.Platform.SystemModules.Queries.GetUserModules;

public record GetUserModulesQuery : IRequest<Result<List<UserModuleDto>>>;

public class GetUserModulesQueryHandler : IRequestHandler<GetUserModulesQuery, Result<List<UserModuleDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;

    public GetUserModulesQueryHandler(
        IApplicationDbContext context,
        IPermissionService permissionService,
        IIdentityService identityService,
        IUser user)
    {
        _context = context;
        _permissionService = permissionService;
        _identityService = identityService;
        _user = user;
    }

    public async Task<Result<List<UserModuleDto>>> Handle(GetUserModulesQuery request, CancellationToken cancellationToken)
    {
        if (!_user.Id.HasValue)
            return Result<List<UserModuleDto>>.Failure(new[] { "Usuário não autenticado." });

        var userId = _user.Id.Value;

        // Verifica se é superadmin
        var isSuperUser = await _identityService.IsSuperUserAsync(userId);

        // Iniciamos a query base
        var query = _context.SystemModules.AsNoTracking();

        // Se NÃO for superadmin, aplicamos o filtro de permissões
        if (!isSuperUser)
        {
            var allowedModuleIds = await _permissionService.GetAllowedModuleIdsAsync(userId, cancellationToken);
            query = query.Where(m => allowedModuleIds.Contains(m.Id));
        }

        // Executamos a query
        var modules = await query
            .OrderBy(m => m.SortOrder)
            .Select(x => new UserModuleDto(
                x.Id,
                x.Name,
                x.Description,
                x.RoutePrefix,
                x.SortOrder,
                x.SystemIconId,
                x.HexColor
            ))
            .ToListAsync(cancellationToken);

        return Result<List<UserModuleDto>>.Success(modules);
    }
}

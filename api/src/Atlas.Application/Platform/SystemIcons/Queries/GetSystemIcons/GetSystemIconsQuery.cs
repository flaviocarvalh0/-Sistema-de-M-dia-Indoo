using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Application.Platform.SystemIcons.Dtos;

namespace Atlas.Application.Platform.SystemIcons.Queries.GetSystemIcons;

public record GetSystemIconsQuery : IRequest<Result<PaginatedList<SystemIconDto>>>
{
    public PaginationParams PaginationParams { get; init; } = new();

    public string? SearchTerm { get; init; }
}

public class GetSystemIconsQueryHandler : IRequestHandler<GetSystemIconsQuery, Result<PaginatedList<SystemIconDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetSystemIconsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<SystemIconDto>>> Handle(GetSystemIconsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SystemIcons.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(x => x.Name.ToLower().Contains(request.SearchTerm.ToLower()));
        }

        var orderedQuery = query
            .OrderBy(x => x.Name)
            .Select(x => new SystemIconDto(
                x.Id,
                x.Name,
                x.ContentType,
                x.Extension,
                x.SizeInBytes,
                x.Created,
                EF.Property<uint>(x, "Version")));

        var paginatedIcons = await orderedQuery.ToEntityPaginatedListAsync(
            _context,
            "system_icons",
            request.PaginationParams,
            cancellationToken);

        return Result<PaginatedList<SystemIconDto>>.Success(paginatedIcons);
    }
}

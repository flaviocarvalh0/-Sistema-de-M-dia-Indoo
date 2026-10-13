using Atlas.Application.Platform.SystemIcons.Dtos;

namespace Atlas.Application.Platform.SystemIcons.Queries.GetSystemIconFile;

public record GetSystemIconFileQuery(long Id) : IRequest<Result<SystemIconFileDto>>;

public class GetSystemIconFileQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSystemIconFileQuery, Result<SystemIconFileDto>>
{
    public async Task<Result<SystemIconFileDto>> Handle(
        GetSystemIconFileQuery request,
        CancellationToken cancellationToken)
    {
        var icon = await context.SystemIcons
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new SystemIconFileDto(x.Data, x.ContentType))
            .FirstOrDefaultAsync(cancellationToken);

        if (icon is null)
        {
            return Result<SystemIconFileDto>.Failure(["Ícone não encontrado."]);
        }

        return Result<SystemIconFileDto>.Success(icon);
    }
}

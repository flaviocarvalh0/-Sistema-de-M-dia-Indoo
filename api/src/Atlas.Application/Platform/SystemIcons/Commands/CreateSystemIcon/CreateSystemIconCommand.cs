using Atlas.Application.Platform.SystemIcons.Dtos;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemIcons.Commands.CreateSystemIcon;

public record CreateSystemIconCommand(
    string Name,
    string ContentType,
    string Extension,
    long SizeInBytes,

    [property: LogIgnore]
    byte[] Data) : IRequest<Result<CreatedSystemIconDto>>;

public class CreateSystemIconCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateSystemIconCommand, Result<CreatedSystemIconDto>>
{
    public async Task<Result<CreatedSystemIconDto>> Handle(
        CreateSystemIconCommand request,
        CancellationToken cancellationToken)
    {
        var icon = new SystemIcon
        {
            Name = request.Name,
            ContentType = request.ContentType,
            Extension = request.Extension,
            SizeInBytes = request.SizeInBytes,
            Data = request.Data
        };

        context.SystemIcons.Add(icon);
        await context.SaveChangesAsync(cancellationToken);

        var version = context.Entry(icon).Property<uint>("Version").CurrentValue;

        return Result<CreatedSystemIconDto>.Success(new CreatedSystemIconDto(icon.Id, version));
    }
}

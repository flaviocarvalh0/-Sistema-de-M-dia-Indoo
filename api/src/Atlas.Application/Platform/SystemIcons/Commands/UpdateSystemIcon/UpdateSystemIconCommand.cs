namespace Atlas.Application.Platform.SystemIcons.Commands.UpdateSystemIcon;

public record UpdateSystemIconCommand(
    long Id,
    string Name,
    string? ContentType,
    string? Extension,
    long? SizeInBytes,

    [property: LogIgnore]
    byte[]? Data,
    uint Version) : IRequest<Result<uint>>;

public class UpdateSystemIconCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateSystemIconCommand, Result<uint>>
{
    public async Task<Result<uint>> Handle(
        UpdateSystemIconCommand request,
        CancellationToken cancellationToken)
    {
        var icon = await context.SystemIcons
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (icon is null)
        {
            return Result<uint>.Failure(["Ícone não encontrado no sistema."]);
        }

        icon.Name = request.Name;

        if (request.Data != null && request.Data.Length > 0)
        {
            icon.ContentType = request.ContentType!;
            icon.Extension = request.Extension!;
            icon.SizeInBytes = request.SizeInBytes!.Value;
            icon.Data = request.Data;
        }

        // Injeta a versão original enviada pelo front para verificação no PostgreSQL
        context.Entry(icon).Property<uint>("Version").OriginalValue = request.Version;

        await context.SaveChangesAsync(cancellationToken);

        // Captura a nova versão (xmin) gerada pelo banco após a atualização
        var newVersion = context.Entry(icon).Property<uint>("Version").CurrentValue;

        return Result<uint>.Success(newVersion);
    }
}

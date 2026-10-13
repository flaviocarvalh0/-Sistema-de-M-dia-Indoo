namespace Atlas.Application.Platform.EntityMetadata.Commands.UpdateEntityMetadata;

[Authorize(Policy = PermissionKeys.EntityMetadata.Update)]
public record UpdateEntityMetadataCommand(
    long Id,
    string DisplayLabel,
    int DefaultPageSize,
    bool EnableHistory,
    bool EnableExport,
    string? CodePrefix,
    uint Version
) : IRequest<Result<uint>>;

public class UpdateEntityMetadataCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateEntityMetadataCommand, Result<uint>>
{
    public async Task<Result<uint>> Handle(UpdateEntityMetadataCommand request, CancellationToken ct)
    {
        var entity = await context.EntityMetadata.FindAsync([request.Id], ct);
        if (entity == null)
        {
            return Result<uint>.Failure(["Metadado de entidade não encontrado."]);
        }

        // Aplica a versão original para o EF rastrear o xmin
        context.Entry(entity).Property<uint>("Version").OriginalValue = request.Version;

        entity.DisplayLabel = request.DisplayLabel.Trim();
        entity.DefaultPageSize = request.DefaultPageSize;
        entity.EnableHistory = request.EnableHistory;
        entity.EnableExport = request.EnableExport;

        // Sanitiza o CodePrefix se informado (ex: " fat " -> "FAT")
        entity.CodePrefix = string.IsNullOrWhiteSpace(request.CodePrefix)
            ? null
            : request.CodePrefix.Trim().ToUpperInvariant();

        await context.SaveChangesAsync(ct);

        var newVersion = context.Entry(entity).Property<uint>("Version").CurrentValue;

        return Result<uint>.Success(newVersion);
    }
}

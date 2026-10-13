namespace Atlas.Application.Platform.SystemMenus.Commands.UpdateSystemMenuItem;

public record UpdateSystemMenuItemCommand(
    long Id,
    string? DisplayLabel,
    int SortOrder,
    SystemMenuType MenuType,
    long? ParentId,
    string? IconHexColor,
    long? SystemIconId,
    long? EntityMetadataId,
    uint Version) : IRequest<Result<uint>>;

public class UpdateSystemMenuItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateSystemMenuItemCommand, Result<uint>>
{
    public async Task<Result<uint>> Handle(
        UpdateSystemMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await context.SystemMenuItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (menuItem is null)
        {
            return Result<uint>.Failure(["Item de menu não encontrado."]);
        }

        var finalLabel = request.DisplayLabel?.Trim();

        if (request.MenuType == SystemMenuType.Entity && string.IsNullOrWhiteSpace(finalLabel))
        {
            var metadata = await context.EntityMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.EntityMetadataId, cancellationToken);

            if (metadata is null)
            {
                return Result<uint>.Failure(["A entidade de metadados associada não foi encontrada."]);
            }

            finalLabel = metadata.DisplayLabel;
        }

        if (string.IsNullOrWhiteSpace(finalLabel))
        {
            return Result<uint>.Failure(["O rótulo de exibição deve ser informado."]);
        }

        menuItem.DisplayLabel = finalLabel;
        menuItem.SortOrder = request.SortOrder;
        menuItem.MenuType = request.MenuType;
        menuItem.ParentId = request.ParentId;
        menuItem.IconHexColor = request.IconHexColor;
        menuItem.SystemIconId = request.SystemIconId;
        menuItem.EntityMetadataId = request.MenuType == SystemMenuType.Entity
            ? request.EntityMetadataId
            : null;

        // Injeta a versão original informada pela request para verificação
        context.Entry(menuItem).Property<uint>("Version").OriginalValue = request.Version;

        await context.SaveChangesAsync(cancellationToken);

        // Captura e retorna o novo xmin/Version pós-persistência
        var newVersion = context.Entry(menuItem).Property<uint>("Version").CurrentValue;

        return Result<uint>.Success(newVersion);
    }
}

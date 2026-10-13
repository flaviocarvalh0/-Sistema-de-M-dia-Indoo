namespace Atlas.Application.Platform.SystemMenus.Commands.MoveSystemMenuItem;

public record MoveSystemMenuItemCommand(
    long Id,
    long? ParentId,
    uint Version) : IRequest<Result<uint>>;

public class MoveSystemMenuItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<MoveSystemMenuItemCommand, Result<uint>>
{
    public async Task<Result<uint>> Handle(
        MoveSystemMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await context.SystemMenuItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (menuItem is null)
        {
            return Result<uint>.Failure(["Item de menu não encontrado."]);
        }

        menuItem.ParentId = request.ParentId;

        context.Entry(menuItem).Property<uint>("Version").OriginalValue = request.Version;

        await context.SaveChangesAsync(cancellationToken);

        var newVersion = context.Entry(menuItem).Property<uint>("Version").CurrentValue;

        return Result<uint>.Success(newVersion);
    }
}

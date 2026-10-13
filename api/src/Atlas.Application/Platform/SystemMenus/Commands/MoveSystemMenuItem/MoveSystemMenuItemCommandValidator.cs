namespace Atlas.Application.Platform.SystemMenus.Commands.MoveSystemMenuItem;

public class MoveSystemMenuItemCommandValidator : AbstractValidator<MoveSystemMenuItemCommand>
{
    public MoveSystemMenuItemCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("O item de menu informado é inválido.")
            .MustAsync(async (id, cancellationToken) =>
                await context.SystemMenuItems.AnyAsync(x => x.Id == id, cancellationToken))
            .WithMessage("O item de menu não foi encontrado.");

        // Garantir que a versão veio informada para o controle otimista de concorrência
        RuleFor(x => x.Version)
            .GreaterThan(0u)
            .WithMessage("A versão do item é obrigatória para o controle de concorrência.");

        RuleFor(x => x)
            .Must(x => x.ParentId != x.Id)
            .WithMessage("Um item de menu não pode ser pai de si mesmo.");

        RuleFor(x => x)
            .MustAsync(async (command, cancellationToken) =>
            {
                if (command.ParentId is null)
                {
                    return true;
                }

                var currentItem = await context.SystemMenuItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == command.Id, cancellationToken);

                if (currentItem is null)
                {
                    return false;
                }

                var parent = await context.SystemMenuItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == command.ParentId, cancellationToken);

                return parent is not null &&
                       parent.SystemModuleId == currentItem.SystemModuleId &&
                       parent.MenuType == SystemMenuType.Menu;
            })
            .WithMessage("O item pai deve existir, pertencer ao mesmo módulo e ser do tipo Menu.");

        RuleFor(x => x)
            .MustAsync(async (command, cancellationToken) =>
                !await WouldCreateCycleAsync(context, command.Id, command.ParentId, cancellationToken))
            .WithMessage("A movimentação criaria um ciclo na árvore de menus.");
    }

    private static async Task<bool> WouldCreateCycleAsync(
        IApplicationDbContext context,
        long itemId,
        long? newParentId,
        CancellationToken cancellationToken)
    {
        if (newParentId is null)
        {
            return false;
        }

        var currentParentId = newParentId;

        while (currentParentId is not null)
        {
            if (currentParentId == itemId)
            {
                return true;
            }

            currentParentId = await context.SystemMenuItems
                .AsNoTracking()
                .Where(x => x.Id == currentParentId)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }
}

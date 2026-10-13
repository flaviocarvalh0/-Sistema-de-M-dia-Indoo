namespace Atlas.Application.Platform.SystemMenus.Commands.UpdateSystemMenuItem;

public class UpdateSystemMenuItemCommandValidator : AbstractValidator<UpdateSystemMenuItemCommand>
{
    public UpdateSystemMenuItemCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("O item de menu informado é inválido.")
            .MustAsync(async (id, cancellationToken) =>
                await context.SystemMenuItems.AnyAsync(x => x.Id == id, cancellationToken))
            .WithMessage("O item de menu não foi encontrado.");

        RuleFor(x => x.Version)
            .GreaterThan(0u)
            .WithMessage("A versão do item de menu é obrigatória para o controle de concorrência.");

        RuleFor(x => x.MenuType)
            .IsInEnum()
            .WithMessage("O tipo do item de menu informado é inválido.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("A ordem de exibição deve ser maior ou igual a zero.");

        RuleFor(x => x.DisplayLabel)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayLabel));

        RuleFor(x => x.EntityMetadataId)
            .NotNull()
            .When(x => x.MenuType == SystemMenuType.Entity)
            .WithMessage("Para itens do tipo Entity, a associação com uma entidade é obrigatória.");

        RuleFor(x => x.EntityMetadataId)
            .Null()
            .When(x => x.MenuType == SystemMenuType.Menu)
            .WithMessage("Itens do tipo Menu não devem possuir entidade associada.");

        RuleFor(x => x.SystemIconId)
            .MustAsync(async (iconId, cancellationToken) =>
                iconId is null || await context.SystemIcons.AnyAsync(x => x.Id == iconId, cancellationToken))
            .WithMessage("O ícone informado não foi encontrado.");

        RuleFor(x => x.EntityMetadataId)
            .MustAsync(async (metadataId, cancellationToken) =>
                metadataId is null || await context.EntityMetadata.AnyAsync(x => x.Id == metadataId, cancellationToken))
            .WithMessage("A entidade de metadados informada não foi encontrada.");

        RuleFor(x => x)
            .MustAsync(async (command, cancellationToken) =>
            {
                if (command.ParentId is null)
                {
                    return true;
                }

                if (command.ParentId == command.Id)
                {
                    return false;
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
            .WithMessage("A alteração do item pai criaria um ciclo na árvore de menus.");
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

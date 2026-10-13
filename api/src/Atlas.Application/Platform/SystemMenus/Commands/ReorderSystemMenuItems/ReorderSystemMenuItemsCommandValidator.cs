namespace Atlas.Application.Platform.SystemMenus.Commands.ReorderSystemMenuItems;

public class ReorderSystemMenuItemsCommandValidator : AbstractValidator<ReorderSystemMenuItemsCommand>
{
    public ReorderSystemMenuItemsCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.SystemModuleId)
            .GreaterThan(0)
            .WithMessage("O módulo informado é inválido.")
            .MustAsync(async (moduleId, cancellationToken) =>
                await context.SystemModules.AnyAsync(x => x.Id == moduleId, cancellationToken))
            .WithMessage("O módulo informado não foi encontrado.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("A lista de itens para reordenação deve ser informada.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithMessage("O item de menu informado é inválido.");

            item.RuleFor(x => x.SortOrder)
                .GreaterThanOrEqualTo(0)
                .WithMessage("A ordem de exibição deve ser maior ou igual a zero.");

            // Validação da versão para garantir que o token de concorrência venha preenchido em cada item do lote
            item.RuleFor(x => x.Version)
                .GreaterThan(0u)
                .WithMessage("A versão do item de menu é obrigatória para o controle de concorrência.");
        });

        RuleFor(x => x)
            .Must(command =>
                command.Items.Select(i => i.Id).Distinct().Count() == command.Items.Count)
            .WithMessage("A lista de reordenação contém itens duplicados.");

        RuleFor(x => x)
            .Must(command =>
                command.Items.All(i => i.ParentId != i.Id))
            .WithMessage("Um item de menu não pode ser pai de si mesmo.");

        RuleFor(x => x)
            .MustAsync(async (command, cancellationToken) =>
                await AllParentIdsAreValidAsync(context, command, cancellationToken))
            .WithMessage("Um ou mais pais informados são inválidos, pertencem a outro módulo ou não são do tipo Menu.");

        RuleFor(x => x)
            .Must(command => !HasCycles(command.Items))
            .WithMessage("A nova organização contém ciclos na árvore de menus.");
    }

    private static async Task<bool> AllParentIdsAreValidAsync(
        IApplicationDbContext context,
        ReorderSystemMenuItemsCommand command,
        CancellationToken cancellationToken)
    {
        var parentIds = command.Items
            .Where(x => x.ParentId is not null)
            .Select(x => x.ParentId!.Value)
            .Distinct()
            .ToList();

        if (parentIds.Count == 0)
        {
            return true;
        }

        var validParentCount = await context.SystemMenuItems
            .AsNoTracking()
            .CountAsync(x =>
                x.SystemModuleId == command.SystemModuleId &&
                parentIds.Contains(x.Id) &&
                x.MenuType == SystemMenuType.Menu,
                cancellationToken);

        return validParentCount == parentIds.Count;
    }

    private static bool HasCycles(List<ReorderSystemMenuItemDto> items)
    {
        var parentMap = items.ToDictionary(x => x.Id, x => x.ParentId);

        foreach (var item in items)
        {
            var visited = new HashSet<long>();
            var currentParentId = item.ParentId;

            while (currentParentId is not null)
            {
                if (!visited.Add(currentParentId.Value))
                {
                    return true;
                }

                if (currentParentId == item.Id)
                {
                    return true;
                }

                if (!parentMap.TryGetValue(currentParentId.Value, out currentParentId))
                {
                    break;
                }
            }
        }

        return false;
    }
}

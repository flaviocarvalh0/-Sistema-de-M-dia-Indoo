namespace Atlas.Application.Platform.SystemMenus.Commands.CreateSystemMenuItem;

public class CreateSystemMenuItemCommandValidator : AbstractValidator<CreateSystemMenuItemCommand>
{
    public CreateSystemMenuItemCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.SystemModuleId)
            .GreaterThan(0)
            .WithMessage("O item de menu deve estar vinculado a um módulo válido.")
            .MustAsync(async (moduleId, cancellationToken) =>
                await context.SystemModules.AnyAsync(x => x.Id == moduleId, cancellationToken))
            .WithMessage("O módulo informado não foi encontrado.");

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
            .WithMessage("Para itens de menu do tipo Entity, a associação com uma entidade é obrigatória.");

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

                var parent = await context.SystemMenuItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == command.ParentId, cancellationToken);

                return parent is not null &&
                       parent.SystemModuleId == command.SystemModuleId &&
                       parent.MenuType == SystemMenuType.Menu;
            })
            .WithMessage("O item pai informado deve existir, pertencer ao mesmo módulo e ser do tipo Menu.");
    }
}
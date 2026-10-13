namespace Atlas.Application.Platform.SystemMenus.Commands.DeleteSystemMenuItem;

public class DeleteSystemMenuItemCommandValidator : AbstractValidator<DeleteSystemMenuItemCommand>
{
    public DeleteSystemMenuItemCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("O item de menu informado é inválido.")
            .MustAsync(async (id, cancellationToken) =>
                await context.SystemMenuItems.AnyAsync(x => x.Id == id, cancellationToken))
            .WithMessage("O item de menu não foi encontrado.");

        RuleFor(x => x.Version)
            .GreaterThan(0u)
            .WithMessage("A versão do item é obrigatória para o controle de concorrência.");
    }
}

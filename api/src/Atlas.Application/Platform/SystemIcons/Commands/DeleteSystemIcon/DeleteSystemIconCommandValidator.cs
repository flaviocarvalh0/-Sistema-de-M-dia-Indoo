namespace Atlas.Application.Platform.SystemIcons.Commands.DeleteSystemIcon;
public class DeleteSystemIconCommandValidator : AbstractValidator<DeleteSystemIconCommand>
{
    public DeleteSystemIconCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("O identificador do ícone é inválido.");

        RuleFor(x => x.Version)
            .GreaterThan(0u)
            .WithMessage("A versão do registro é obrigatória para exclusão.");
    }
}

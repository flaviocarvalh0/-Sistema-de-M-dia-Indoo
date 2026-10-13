namespace Atlas.Application.Platform.SystemParameters.Commands.DeleteSystemParameter;
public class DeleteSystemParameterCommandValidator : AbstractValidator<DeleteSystemParameterCommand>
{
    public DeleteSystemParameterCommandValidator()
    {
        RuleFor(v => v.Key)
            .NotEmpty().WithMessage("A chave do parâmetro é obrigatória.");

        RuleFor(v => v.Version)
            .GreaterThan(0u).WithMessage("A versão é obrigatória para efetuar a exclusão com segurança.");
    }
}

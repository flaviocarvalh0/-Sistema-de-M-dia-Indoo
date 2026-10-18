using Atlas.Application.Business.Rede.Clientes.Commands.Common;

namespace Atlas.Application.Business.Rede.Clientes.Commands.CriarCliente;

public class CriarClienteCommandValidator : AbstractValidator<CriarClienteCommand>
{
    public CriarClienteCommandValidator()
    {
        RuleFor(x => x.Nome).NomeCliente();

        RuleFor(x => x.NomeResponsavel)
            .NotEmpty().WithMessage("O nome do responsável é obrigatório.")
            .MaximumLength(150).WithMessage("O nome do responsável deve ter no máximo 150 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(254).WithMessage("O e-mail deve ter no máximo 254 caracteres.")
            .EmailAddress().WithMessage("O formato do e-mail é inválido.");

        RuleFor(x => x.Telefone).TelefoneCliente();
        RuleFor(x => x.Documento).DocumentoCliente();
        RuleFor(x => x.Cidade).CidadeCliente();
        RuleFor(x => x.FusoHorario).FusoHorarioCliente();
        RuleFor(x => x.CotaArmazenamentoMb).CotaCliente();
        RuleFor(x => x.LimiteMidiasAtivas).LimiteMidiasAtivas();
    }
}

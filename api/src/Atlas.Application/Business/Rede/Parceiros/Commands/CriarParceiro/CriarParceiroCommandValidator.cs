using Atlas.Application.Business.Rede.Parceiros.Commands.Common;

namespace Atlas.Application.Business.Rede.Parceiros.Commands.CriarParceiro;

public class CriarParceiroCommandValidator : AbstractValidator<CriarParceiroCommand>
{
    public CriarParceiroCommandValidator()
    {
        RuleFor(x => x.Nome).NomeParceiro();

        RuleFor(x => x.NomeResponsavel)
            .NotEmpty().WithMessage("O nome do responsável é obrigatório.")
            .MaximumLength(150).WithMessage("O nome do responsável deve ter no máximo 150 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(254).WithMessage("O e-mail deve ter no máximo 254 caracteres.")
            .EmailAddress().WithMessage("O formato do e-mail é inválido.");

        RuleFor(x => x.Telefone).TelefoneParceiro();
        RuleFor(x => x.Documento).DocumentoParceiro();
        RuleFor(x => x.Cidade).CidadeParceiro();
        RuleFor(x => x.FusoHorario).FusoHorarioParceiro();
        RuleFor(x => x.CotaArmazenamentoMb).CotaParceiro();
        RuleFor(x => x.LimiteMidiasAtivas).LimiteMidiasAtivas();
    }
}

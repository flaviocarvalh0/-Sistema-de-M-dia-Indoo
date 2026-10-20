using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Clientes.Common;
using Atlas.Application.Business.Veiculacao;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Indoor.Midias.Commands.AtualizarMidia;

/// <summary>
/// Título, duração, período de exibição e o QR code exibido por cima da mídia
/// (sem <c>QrTipo</c>, a mídia fica sem QR code). O arquivo muda em "Substituir arquivo".
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Update)]
public record AtualizarMidiaCommand : IRequest<uint>, IQrCodeInformado
{
    public long Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public int DuracaoSegundos { get; init; }
    public DateOnly? InicioExibicao { get; init; }
    public DateOnly? FimExibicao { get; init; }

    public TipoQrCode? QrTipo { get; init; }
    public string? QrDestino { get; init; }
    public string? QrMensagem { get; init; }
    public decimal? QrValor { get; init; }
    public string? QrNomeRecebedor { get; init; }
    public string? QrCidadeRecebedor { get; init; }
    public string? QrLegenda { get; init; }
    public PosicaoQrCode? QrPosicao { get; init; }
    public TamanhoQrCode? QrTamanho { get; init; }

    public uint Version { get; init; }
}

public class AtualizarMidiaCommandHandler(IApplicationDbContext context, IClienteAtual clienteAtual)
    : IRequestHandler<AtualizarMidiaCommand, uint>
{
    public async Task<uint> Handle(AtualizarMidiaCommand request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);
        var midia = await RegistrosDoCliente.CarregarAsync(context.Midias, cliente.ClienteId, request.Id, cancellationToken);

        // Valida e monta antes de mexer na mídia: erro de QR code não grava nada.
        var qrCode = MontadorQrCode.Montar(request);

        context.ConferirVersao(midia, request.Version);

        midia.Titulo = request.Titulo.Trim();
        midia.DuracaoSegundos = request.DuracaoSegundos;
        midia.InicioExibicao = request.InicioExibicao;
        midia.FimExibicao = request.FimExibicao;

        if (qrCode is null)
        {
            midia.RemoverQrCode();
        }
        else
        {
            midia.DefinirQrCode(qrCode);
        }

        await LinkQr.AtualizarCodigoAsync(context, midia, cancellationToken);

        return await context.SalvarAsync(midia, cancellationToken);
    }
}

public class AtualizarMidiaCommandValidator : AbstractValidator<AtualizarMidiaCommand>
{
    public AtualizarMidiaCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da mídia é obrigatório.");
        RuleFor(x => x.Titulo).TituloMidia();
        RuleFor(x => x.DuracaoSegundos).DuracaoMidia();
        RuleFor(x => x).PeriodoMidia(x => x.InicioExibicao, x => x.FimExibicao);
        RuleFor(x => x.QrTipo).IsInEnum().When(x => x.QrTipo is not null).WithMessage("Tipo de QR code inválido.");
        RuleFor(x => x.QrPosicao).IsInEnum().When(x => x.QrPosicao is not null).WithMessage("Posição do QR code inválida.");
        RuleFor(x => x.QrTamanho).IsInEnum().When(x => x.QrTamanho is not null).WithMessage("Tamanho do QR code inválido.");
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

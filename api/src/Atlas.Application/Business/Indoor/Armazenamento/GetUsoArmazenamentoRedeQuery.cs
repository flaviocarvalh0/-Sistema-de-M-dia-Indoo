using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>Uso do storage da rede inteira contra o teto (ADR-025), para a operadora.</summary>
[Authorize(Policy = BusinessPermissionKeys.Clientes.View)]
public record GetUsoArmazenamentoRedeQuery : IRequest<UsoArmazenamentoRedeDto>;

public class GetUsoArmazenamentoRedeQueryHandler(TetoArmazenamento teto)
    : IRequestHandler<GetUsoArmazenamentoRedeQuery, UsoArmazenamentoRedeDto>
{
    public Task<UsoArmazenamentoRedeDto> Handle(GetUsoArmazenamentoRedeQuery request, CancellationToken cancellationToken) =>
        teto.MedirAsync(cancellationToken);
}

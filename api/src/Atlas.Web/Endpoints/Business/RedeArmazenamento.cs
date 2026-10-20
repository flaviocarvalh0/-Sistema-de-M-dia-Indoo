using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Business;

/// <summary>Uso do storage da rede contra o teto de 10 GB (ADR-025).</summary>
public class RedeArmazenamento : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/armazenamento";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Armazenamento")
            .WithDescription("Quanto do storage a rede usa e quanto falta para o teto.");

        groupBuilder
            .MapGet(GetUsoArmazenamentoRede)
            .RequireAuthorization(BusinessPermissionKeys.Clientes.View);
    }

    [EndpointSummary("Uso do armazenamento da rede")]
    [EndpointDescription("Soma do banco, envios em andamento, última medição do bucket e o teto configurado (no máximo 10 GB).")]
    public static async Task<Ok<UsoArmazenamentoRedeDto>> GetUsoArmazenamentoRede(ISender sender, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetUsoArmazenamentoRedeQuery(), cancellationToken));
    }
}

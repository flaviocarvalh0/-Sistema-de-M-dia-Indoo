using Atlas.Application.Business.Veiculacao;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>Relatório de veiculação do próprio cliente (ADR-023).</summary>
public class ClienteRelatorio : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/cliente/relatorio";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Cliente: Relatório")
            .WithDescription("Onde e quantas vezes os anúncios do cliente passaram e quantas pessoas leram o QR.");

        groupBuilder.MapGet(GetRelatorio).RequireAuthorization(BusinessPermissionKeys.Midias.View);
    }

    [EndpointSummary("Relatório de veiculação")]
    [EndpointDescription("Exibições registradas pelas TVs (por TV, mídia, dia e hora local) e leituras do QR rastreado. Sem datas: do dia 1 do mês até hoje. Máximo de 93 dias.")]
    public static async Task<Ok<RelatorioVeiculacaoDto>> GetRelatorio(
        ISender sender, [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetMeuRelatorioVeiculacaoQuery(de, ate), cancellationToken));
    }
}

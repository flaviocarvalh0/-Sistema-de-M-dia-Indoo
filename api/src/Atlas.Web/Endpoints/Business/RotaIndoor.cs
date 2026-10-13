using ValidationException = Atlas.Application.Common.Exceptions.ValidationException;

namespace Atlas.Web.Endpoints.Business;

/// <summary>Regras de contrato comuns aos endpoints da Mídia Indoor.</summary>
internal static class RotaIndoor
{
    /// <summary>O id da rota repete o do corpo; divergência é erro do cliente (400).</summary>
    public static void GarantirMesmoId(long idRota, long idCorpo)
    {
        if (idRota != idCorpo)
        {
            throw new ValidationException("Id", ["O ID da rota é diferente do ID informado no corpo."]);
        }
    }
}

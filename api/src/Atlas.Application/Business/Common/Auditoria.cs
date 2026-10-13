namespace Atlas.Application.Business.Common;

/// <summary>
/// Campos de auditoria dos DTOs de negócio, no formato que a listagem e o
/// rodapé dos modais do @atlas/framework leem (<c>created</c>,
/// <c>createdByName</c>, <c>lastModified</c>, <c>lastModifiedByName</c>).
/// </summary>
public interface IDtoAuditavel
{
    long? CreatedBy { get; }
    string? CreatedByName { get; set; }
    long? LastModifiedBy { get; }
    string? LastModifiedByName { get; set; }
}

public static class Auditoria
{
    /// <summary>Preenche os nomes dos usuários com uma consulta só para a página inteira.</summary>
    public static async Task PreencherNomesAsync(
        this IIdentityService identityService,
        IEnumerable<IDtoAuditavel> dtos,
        CancellationToken cancellationToken)
    {
        var lista = dtos.ToList();

        var ids = lista
            .SelectMany(d => new[] { d.CreatedBy, d.LastModifiedBy })
            .OfType<long>()
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        var nomes = await identityService.GetUserNamesAsync(ids, cancellationToken);

        foreach (var dto in lista)
        {
            dto.CreatedByName = dto.CreatedBy is { } criador ? nomes.GetValueOrDefault(criador) : null;
            dto.LastModifiedByName = dto.LastModifiedBy is { } editor ? nomes.GetValueOrDefault(editor) : null;
        }
    }
}

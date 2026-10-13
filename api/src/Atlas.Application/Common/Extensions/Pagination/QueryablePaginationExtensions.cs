using Atlas.Application.Common.Models.Pagination;

namespace Atlas.Application.Common.Extensions.Pagination;
public static class QueryablePaginationExtensions
{
    /// <summary>
    /// Aplica a paginação em um IQueryable consultando dinamicamente o tamanho padrão 
    /// de página configurado nos metadados da entidade (`EntityMetadata`). 
    /// Caso nenhum metadado seja encontrado, aplica o fallback padrão de 100 registros.
    /// </summary>
    /// <typeparam name="TDestination">Tipo do DTO de destino.</typeparam>
    /// <param name="query">A query base do Entity Framework.</param>
    /// <param name="context">O contexto do banco de dados da aplicação.</param>
    /// <param name="tableName">O nome técnico da tabela correspondente no EntityMetadata.</param>
    /// <param name="paginationParams">Parâmetros de paginação vindos da requisição (pode sobrescrever o padrão).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Uma lista paginada contendo os itens e os metadados de paginação.</returns>
    public static async Task<PaginatedList<TDestination>> ToEntityPaginatedListAsync<TDestination>(
        this IQueryable<TDestination> query,
        IApplicationDbContext context,
        string tableName,
        PaginationParams paginationParams,
        CancellationToken cancellationToken = default)
    {
        // 1. Busca o DefaultPageSize configurado nos metadados da entidade no banco
        var metadata = await context.EntityMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TableName == tableName, cancellationToken);

        var defaultPageSize = metadata?.DefaultPageSize ?? 100;

        // 2. Define o tamanho final priorizando os parâmetros da requisição
        var pageSize = paginationParams.PageSize > 0
            ? paginationParams.PageSize
            : defaultPageSize;

        var pageNumber = paginationParams.PageNumber > 0
            ? paginationParams.PageNumber
            : 1;

        // 3. Executa a paginação utilizando a infraestrutura base
        return await query.ToPaginatedListAsync(pageNumber, pageSize);
    }
}

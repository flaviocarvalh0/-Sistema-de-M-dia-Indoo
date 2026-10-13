using Atlas.Application.Common.Models.Pagination;

namespace Atlas.Application.Common.Extensions.Pagination;

/// <summary>
/// Fornece métodos de extensão para facilitar a aplicação de paginação em consultas IQueryable do Entity Framework.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Converte um IQueryable em uma lista paginada de forma assíncrona utilizando números diretos de página e tamanho.
    /// </summary>
    /// <typeparam name="T">O tipo dos elementos da consulta.</typeparam>
    /// <param name="source">A fonte de dados IQueryable a ser paginada.</param>
    /// <param name="pageNumber">O número da página atual (começando em 1).</param>
    /// <param name="pageSize">A quantidade de itens por página.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona, contendo o resultado paginado.</returns>
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> source,
        int pageNumber,
        int pageSize)
    {
        return await PaginatedList<T>.CreateAsync(source, pageNumber, pageSize);
    }

    /// <summary>
    /// Converte um IQueryable em uma lista paginada de forma assíncrona utilizando um objeto de parâmetros de paginação.
    /// </summary>
    /// <typeparam name="T">O tipo dos elementos da consulta.</typeparam>
    /// <param name="source">A fonte de dados IQueryable a ser paginada.</param>
    /// <param name="paginationParams">O objeto contendo o número e o tamanho da página validados.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona, contendo o resultado paginado.</returns>
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> source,
        PaginationParams paginationParams)
    {
        return await PaginatedList<T>.CreateAsync(
            source,
            paginationParams.PageNumber,
            paginationParams.PageSize
        );
    }
}

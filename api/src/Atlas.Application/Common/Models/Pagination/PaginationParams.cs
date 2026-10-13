namespace Atlas.Application.Common.Models.Pagination;
public class PaginationParams
{
    private const int MaxPageSize = 15000;
    private int _pageSize;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : (value < 0 ? 0 : value);
    }
}

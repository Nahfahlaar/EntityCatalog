namespace EntityCatalog.Dtos;

public class GetProductsQueryDto
{
    public Guid? CategoryId { get; set; }
    public Guid? TagId { get; set; }
    public string? Search { get; set; }
    public string? CreatedFrom { get; set; }
    public string? CreatedTo { get; set; }
    public string? Sort { get; set; }
    public string? Order { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

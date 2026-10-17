namespace EntityCatalog.Dtos;

public class ProductImageDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = default!;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
}

namespace EntityCatalog.Products;

public class Product
{
    public Guid Id { get; set; }
    public string ExternalKey { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ProductImage> Images { get; set; } = [];
}

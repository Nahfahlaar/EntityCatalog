namespace EntityCatalog.Dtos;

public class GetProductDto
{
    public Guid Id { get; set; }
    public string ExternalKey { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<Guid> CategoryIds { get; set; } = [];
    public List<Guid> TagIds { get; set; } = [];
    public List<ProductImageDto> Images { get; set; } = [];
}

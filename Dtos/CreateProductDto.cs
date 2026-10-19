using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

public class CreateProductDto
{
    [Required]
    [MaxLength(64)]
    public string ExternalKey { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = default!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [ProductPriceRange]
    public decimal? Price { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> CategoryIds { get; set; } = [];

    public List<Guid> TagIds { get; set; } = [];

    [Required]
    public List<ProductImageInputDto> Images { get; set; } = [];
}

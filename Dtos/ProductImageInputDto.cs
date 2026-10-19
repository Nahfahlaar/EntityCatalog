using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

public class ProductImageInputDto : IValidatableObject
{
    [Required]
    [StringLength(2048)]
    public string Url { get; set; } = default!;

    [StringLength(500)]
    public string? AltText { get; set; }

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "Url must be an absolute HTTP or HTTPS URL.",
                [nameof(Url)]);
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

public class SetProductPublicationDto
{
    [Required]
    public bool? IsPublished { get; set; }
}

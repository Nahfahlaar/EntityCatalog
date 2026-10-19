using System;
using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

public class CreateCategoryDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;
}

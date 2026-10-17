using System;
using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

public class CreateTagDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;
}

using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

public class LoginRequestDto
{
    [Required]
    [MaxLength(256)]
    public string UserName { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}

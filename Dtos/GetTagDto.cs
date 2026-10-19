using System;
using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

public class GetTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
}

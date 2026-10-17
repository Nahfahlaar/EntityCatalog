using System.ComponentModel.DataAnnotations;

namespace EntityCatalog.Dtos;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ProductPriceRangeAttribute : ValidationAttribute
{
    private const decimal MaximumPrice = 9999999999999999.99m;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is decimal price && price >= 0 && price <= MaximumPrice)
            return ValidationResult.Success;

        return new ValidationResult(
            ErrorMessage ?? "Price must be between 0 and 9999999999999999.99.");
    }
}

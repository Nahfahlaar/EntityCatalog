using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using EntityCatalog.Dtos;
using EntityCatalog.Services;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;

namespace EntityCatalog.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] GetProductsQueryDto query)
    {
        if (query.Page < 1)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid page",
                detail: "Page must be greater than or equal to 1.");

        if (query.PageSize < 1 || query.PageSize > 100)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid pageSize",
                detail: "PageSize must be between 1 and 100.");

        if (query.Page > (long)int.MaxValue / query.PageSize + 1)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid page",
                detail: "The requested page is too large.");

        var validSorts = new[] { "name", "createdat", "updatedat" };

        if (!string.IsNullOrWhiteSpace(query.Sort) &&
            !validSorts.Contains(query.Sort, StringComparer.OrdinalIgnoreCase))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid sort",
                detail: "Sort must be name, createdAt or updatedAt.");
        }

        var validOrders = new[] { "asc", "desc" };

        if (!string.IsNullOrWhiteSpace(query.Order) &&
            !validOrders.Contains(query.Order, StringComparer.OrdinalIgnoreCase))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid order",
                detail: "Order must be asc or desc.");
        }

        if (!TryParseUtcTimestamp(query.CreatedFrom, out var createdFromUtc))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid createdFrom",
                detail: "createdFrom must be a valid date/time value.");

        if (!TryParseUtcTimestamp(query.CreatedTo, out var createdToUtc))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid createdTo",
                detail: "createdTo must be a valid date/time value.");

        if (createdFromUtc.HasValue && createdToUtc.HasValue &&
            createdFromUtc.Value > createdToUtc.Value)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date range",
                detail: "createdFrom must be less than or equal to createdTo.");
        }

        var result = await _productService.GetAsync(
            query, createdFromUtc, createdToUtc,
            includeUnpublished: User.IsInRole("Editor"));

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetId(Guid id)
    {
        var product = await _productService.GetByIdAsync(
            id, includeUnpublished: User.IsInRole("Editor"));

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Create(CreateProductDto dto)
    {
        try
        {
            var product = await _productService.CreateAsync(dto);

            if (product == null)
                return NotFound();

            return Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Product conflict",
                detail: ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid product data",
                detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Update(Guid id, CreateProductDto dto)
    {
        try
        {
            var product = await _productService.UpdateAsync(id, dto);

            if (product == null)
                return NotFound();

            return Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Product conflict",
                detail: ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid product data",
                detail: ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _productService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPut("{id:guid}/publication")]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> SetPublication(Guid id, SetProductPublicationDto dto)
    {
        var updated = await _productService.SetPublicationAsync(id, dto.IsPublished!.Value);
        return updated ? NoContent() : NotFound();
    }

    private static bool TryParseUtcTimestamp(string? value, out DateTime? utcValue)
    {
        utcValue = null;

        if (value is null)
            return true;

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        utcValue = parsed.UtcDateTime;
        return true;
    }
}

using EntityCatalog.Dtos;
using EntityCatalog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EntityCatalog.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly CategoryService _categoryService;

    public CategoriesController(CategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var categories = await _categoryService.GetAsync();
        return Ok(categories);
    }

    [HttpPost]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Create(CreateCategoryDto dto)
    {
        try
        {
            var category = await _categoryService.CreateAsync(dto);

            if (category == null)
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Category conflict",
                    detail: "A category with this name already exists.");

            return Ok(category);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Category conflict",
                detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Update(Guid id, CreateCategoryDto dto)
    {
        try
        {
            var category = await _categoryService.UpdateAsync(id, dto);
            return category is null ? NotFound() : Ok(category);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Category conflict",
                detail: ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _categoryService.DeleteAsync(id);
        return result switch
        {
            ReferenceDeleteResult.Deleted => NoContent(),
            ReferenceDeleteResult.NotFound => NotFound(),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Category is in use",
                detail: "A category assigned to a product cannot be deleted.")
        };
    }
}

using EntityCatalog.Dtos;
using EntityCatalog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EntityCatalog.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly TagService _tagService;

    public TagsController(TagService tagService)
    {
        _tagService = tagService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var tags = await _tagService.GetAsync();
        return Ok(tags);
    }

    [HttpPost]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Create(CreateTagDto dto)
    {
        try
        {
            var tag = await _tagService.CreateAsync(dto);

            if (tag == null)
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Tag conflict",
                    detail: "A tag with this name already exists.");

            return Ok(tag);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Tag conflict",
                detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Update(Guid id, CreateTagDto dto)
    {
        try
        {
            var tag = await _tagService.UpdateAsync(id, dto);
            return tag is null ? NotFound() : Ok(tag);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Tag conflict",
                detail: ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Editor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _tagService.DeleteAsync(id);
        return result switch
        {
            ReferenceDeleteResult.Deleted => NoContent(),
            ReferenceDeleteResult.NotFound => NotFound(),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Tag is in use",
                detail: "A tag assigned to a product cannot be deleted.")
        };
    }
}

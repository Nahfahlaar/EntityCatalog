using EntityCatalog.Data;
using EntityCatalog.Dtos;
using EntityCatalog.Products;
using Microsoft.EntityFrameworkCore;

namespace EntityCatalog.Services;

public class TagService
{
    private readonly AppDbContext _context;

    public TagService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GetTagDto>> GetAsync()
    {
        return await _context.Tags
            .AsNoTracking()
            .Select(t => new GetTagDto
            {
                Id = t.Id,
                Name = t.Name
            })
            .ToListAsync();
    }

    public async Task<Tag?> CreateAsync(CreateTagDto dto)
    {
        var nameExists = await _context.Tags
            .AnyAsync(t => t.Name == dto.Name);

        if (nameExists)
            return null;

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = dto.Name
        };

        _context.Tags.Add(tag);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            PostgresUniqueConstraint.IsViolation(ex, "IX_Tags_Name"))
        {
            throw new InvalidOperationException(
                "Tag with this name already exists.", ex);
        }

        return tag;
    }

    public async Task<Tag?> UpdateAsync(Guid id, CreateTagDto dto)
    {
        var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null)
            return null;

        var nameExists = await _context.Tags
            .AnyAsync(t => t.Id != id && t.Name == dto.Name);
        if (nameExists)
            throw new InvalidOperationException("Tag with this name already exists.");

        tag.Name = dto.Name;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            PostgresUniqueConstraint.IsViolation(ex, "IX_Tags_Name"))
        {
            throw new InvalidOperationException(
                "Tag with this name already exists.", ex);
        }

        return tag;
    }

    public async Task<ReferenceDeleteResult> DeleteAsync(Guid id)
    {
        var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null)
            return ReferenceDeleteResult.NotFound;

        if (await _context.ProductTags.AnyAsync(link => link.TagId == id))
            return ReferenceDeleteResult.InUse;

        _context.Tags.Remove(tag);
        try
        {
            await _context.SaveChangesAsync();
            return ReferenceDeleteResult.Deleted;
        }
        catch (DbUpdateException ex) when (
            PostgresForeignKeyViolation.IsViolation(ex, "FK_EntityTags_Tags_TagId"))
        {
            return ReferenceDeleteResult.InUse;
        }
    }
}

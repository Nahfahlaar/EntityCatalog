using EntityCatalog.Data;
using EntityCatalog.Dtos;
using EntityCatalog.Products;
using Microsoft.EntityFrameworkCore;

namespace EntityCatalog.Services;

public class CategoryService
{
    private readonly AppDbContext _context;

    public CategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GetCategoryDto>> GetAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .Select(g => new GetCategoryDto
            {
                Id = g.Id,
                Name = g.Name
            })
            .ToListAsync();
    }

    public async Task<Category?> CreateAsync(CreateCategoryDto dto)
    {
        var nameExists = await _context.Categories
            .AnyAsync(g => g.Name == dto.Name);

        if (nameExists)
            return null;

        var group = new Category
        {
            Id = Guid.NewGuid(),
            Name = dto.Name
        };

        _context.Categories.Add(group);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            PostgresUniqueConstraint.IsViolation(ex, "IX_Groups_Name"))
        {
            throw new InvalidOperationException(
                "Category with this name already exists.", ex);
        }

        return group;
    }

    public async Task<Category?> UpdateAsync(Guid id, CreateCategoryDto dto)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(g => g.Id == id);
        if (category is null)
            return null;

        var nameExists = await _context.Categories
            .AnyAsync(g => g.Id != id && g.Name == dto.Name);
        if (nameExists)
            throw new InvalidOperationException("Category with this name already exists.");

        category.Name = dto.Name;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            PostgresUniqueConstraint.IsViolation(ex, "IX_Groups_Name"))
        {
            throw new InvalidOperationException(
                "Category with this name already exists.", ex);
        }

        return category;
    }

    public async Task<ReferenceDeleteResult> DeleteAsync(Guid id)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(g => g.Id == id);
        if (category is null)
            return ReferenceDeleteResult.NotFound;

        if (await _context.ProductCategories.AnyAsync(link => link.CategoryId == id))
            return ReferenceDeleteResult.InUse;

        _context.Categories.Remove(category);
        try
        {
            await _context.SaveChangesAsync();
            return ReferenceDeleteResult.Deleted;
        }
        catch (DbUpdateException ex) when (
            PostgresForeignKeyViolation.IsViolation(ex, "FK_EntityGroups_Groups_GroupId"))
        {
            return ReferenceDeleteResult.InUse;
        }
    }
}

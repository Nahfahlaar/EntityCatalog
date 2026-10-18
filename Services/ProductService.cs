using Microsoft.EntityFrameworkCore;
using EntityCatalog.Data;
using EntityCatalog.Dtos;
using EntityCatalog.Products;

namespace EntityCatalog.Services;

public class ProductService
{
    private readonly AppDbContext _context;

    public ProductService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GetProductDto>> GetAsync(
        GetProductsQueryDto query,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        bool includeUnpublished = false)
    {
        var entitiesQuery = _context.Products
            .AsNoTracking()
            .AsQueryable();

        if (!includeUnpublished)
            entitiesQuery = entitiesQuery.Where(product => product.IsPublished);

        entitiesQuery = ApplyFilters(
            entitiesQuery, query, createdFromUtc, createdToUtc);
        entitiesQuery = ApplySorting(entitiesQuery, query);

        var entities = await entitiesQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var entityIds = entities.Select(e => e.Id).ToList();

        var groups = await _context.ProductCategories
            .Where(x => entityIds.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.CategoryId })
            .ToListAsync();

        var tags = await _context.ProductTags
            .Where(x => entityIds.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.TagId })
            .ToListAsync();

        var images = await _context.ProductImages
            .AsNoTracking()
            .Where(x => entityIds.Contains(x.ProductId))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var groupsByEntity = groups
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.CategoryId).ToList());

        var tagsByEntity = tags
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.TagId).ToList());

        var imagesByProduct = images
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = entities.Select(entity => new GetProductDto
        {
            Id = entity.Id,
            ExternalKey = entity.ExternalKey,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            IsPublished = entity.IsPublished,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,

            CategoryIds = groupsByEntity.TryGetValue(entity.Id, out var categoryIds)
                ? categoryIds
                : new List<Guid>(),

            TagIds = tagsByEntity.TryGetValue(entity.Id, out var tagIds)
                ? tagIds
                : new List<Guid>(),

            Images = imagesByProduct.TryGetValue(entity.Id, out var productImages)
                ? productImages.Select(ToImageDto).ToList()
                : new List<ProductImageDto>()
        }).ToList();

        return result;
    }

    private IQueryable<Product> ApplyFilters(
        IQueryable<Product> query,
        GetProductsQueryDto filter,
        DateTime? createdFromUtc,
        DateTime? createdToUtc)
    {
        if (filter.CategoryId.HasValue)
        {
            var categoryId = filter.CategoryId.Value;

            query = query.Where(e =>
                _context.ProductCategories.Any(x =>
                    x.ProductId == e.Id &&
                    x.CategoryId == categoryId));
        }

        if (filter.TagId.HasValue)
        {
            var tagId = filter.TagId.Value;

            query = query.Where(e =>
                _context.ProductTags.Any(x =>
                    x.ProductId == e.Id &&
                    x.TagId == tagId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(e =>
                e.Name.Contains(filter.Search));
        }

        if (createdFromUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= createdFromUtc.Value);
        }

        if (createdToUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt <= createdToUtc.Value);
        }

        return query;
    }

    private static IQueryable<Product> ApplySorting(
        IQueryable<Product> query,
        GetProductsQueryDto filter)
    {
        var descending = string.Equals(
            filter.Order,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        var sortedQuery = filter.Sort?.ToLowerInvariant() switch
        {
            "createdat" => descending
                ? query.OrderByDescending(e => e.CreatedAt)
                : query.OrderBy(e => e.CreatedAt),

            "updatedat" => descending
                ? query.OrderByDescending(e => e.UpdatedAt)
                : query.OrderBy(e => e.UpdatedAt),

            _ => descending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name)
        };

        return sortedQuery.ThenBy(e => e.Id);
    }

    public async Task<GetProductDto?> GetByIdAsync(Guid id, bool includeUnpublished = false)
    {
        var productsQuery = _context.Products.AsNoTracking();

        if (!includeUnpublished)
            productsQuery = productsQuery.Where(product => product.IsPublished);

        var product = await productsQuery
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id))
            .FirstOrDefaultAsync(e => e.Id == id);

        return product is null ? null : await ToProductDtoAsync(product);
    }

    public async Task<GetProductDto?> CreateAsync(CreateProductDto dto)
    {
        var price = GetValidatedPrice(dto);
        EnsureCategoryIdsProvided(dto);

        var groups = await _context.Categories
            .Where(g => dto.CategoryIds.Contains(g.Id))
            .ToListAsync();

        var tags = await _context.Tags
            .Where(t => dto.TagIds.Contains(t.Id))
            .ToListAsync();

        if (groups.Count != dto.CategoryIds.Count ||
            tags.Count != dto.TagIds.Count)
        {
            return null;
        }

        var keyExists = await _context.Products
            .AnyAsync(e => e.ExternalKey == dto.ExternalKey);

        if (keyExists)
        {
            throw new InvalidOperationException(
                "Product with this ExternalKey already exists.");
        }

        var now = DateTime.UtcNow;

        var entity = new Product
        {
            Id = Guid.NewGuid(),
            ExternalKey = dto.ExternalKey,
            Name = dto.Name,
            Description = dto.Description,
            Price = price,
            CreatedAt = now,
            UpdatedAt = now,
            Images = dto.Images.Select(CreateProductImage).ToList()
        };

        _context.Products.Add(entity);

        _context.ProductCategories.AddRange(
            groups.Select(g => new ProductCategory
            {
                ProductId = entity.Id,
                CategoryId = g.Id
            }));

        _context.ProductTags.AddRange(
            tags.Select(t => new ProductTag
            {
                ProductId = entity.Id,
                TagId = t.Id
            }));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            PostgresUniqueConstraint.IsViolation(ex, "IX_Entities_ExternalKey"))
        {
            throw new InvalidOperationException(
                "Product with this ExternalKey already exists.", ex);
        }

        return await GetByIdAsync(entity.Id, includeUnpublished: true);
    }

    public async Task<GetProductDto?> UpdateAsync(Guid id, CreateProductDto dto)
    {
        var price = GetValidatedPrice(dto);
        var entity = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity == null)
            return null;

        EnsureCategoryIdsProvided(dto);

        var groups = await _context.Categories
            .Where(g => dto.CategoryIds.Contains(g.Id))
            .ToListAsync();

        var tags = await _context.Tags
            .Where(t => dto.TagIds.Contains(t.Id))
            .ToListAsync();

        if (groups.Count != dto.CategoryIds.Count ||
            tags.Count != dto.TagIds.Count)
        {
            return null;
        }

        var keyExists = await _context.Products
            .AnyAsync(e => e.ExternalKey == dto.ExternalKey && e.Id != id);

        if (keyExists)
        {
            throw new InvalidOperationException(
                "Product with this ExternalKey already exists.");
        }

        var oldGroups = await _context.ProductCategories
            .Where(x => x.ProductId == id)
            .ToListAsync();

        var oldTags = await _context.ProductTags
            .Where(x => x.ProductId == id)
            .ToListAsync();

        _context.ProductCategories.RemoveRange(oldGroups);
        _context.ProductTags.RemoveRange(oldTags);

        entity.ExternalKey = dto.ExternalKey;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = price;
        entity.UpdatedAt = DateTime.UtcNow;

        _context.ProductImages.RemoveRange(entity.Images);
        entity.Images = dto.Images.Select(CreateProductImage).ToList();

        _context.ProductCategories.AddRange(
            groups.Select(g => new ProductCategory
            {
                ProductId = id,
                CategoryId = g.Id
            }));

        _context.ProductTags.AddRange(
            tags.Select(t => new ProductTag
            {
                ProductId = id,
                TagId = t.Id
            }));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            PostgresUniqueConstraint.IsViolation(ex, "IX_Entities_ExternalKey"))
        {
            throw new InvalidOperationException(
                "Product with this ExternalKey already exists.", ex);
        }

        return await GetByIdAsync(entity.Id, includeUnpublished: true);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.Products
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity == null)
            return false;

        var groups = await _context.ProductCategories
            .Where(x => x.ProductId == id)
            .ToListAsync();

        var tags = await _context.ProductTags
            .Where(x => x.ProductId == id)
            .ToListAsync();

        _context.ProductCategories.RemoveRange(groups);
        _context.ProductTags.RemoveRange(tags);
        _context.Products.Remove(entity);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SetPublicationAsync(Guid id, bool isPublished)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
            return false;

        product.IsPublished = isPublished;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private static void EnsureCategoryIdsProvided(CreateProductDto dto)
    {
        if (dto.CategoryIds is null || dto.CategoryIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one CategoryId is required.", nameof(dto.CategoryIds));
        }
    }

    private static decimal GetValidatedPrice(CreateProductDto dto)
    {
        if (!dto.Price.HasValue || dto.Price.Value < 0 ||
            dto.Price.Value > 9999999999999999.99m)
        {
            throw new ArgumentException(
                "Price must be between 0 and 9999999999999999.99.", nameof(dto.Price));
        }

        return dto.Price.Value;
    }

    private static ProductImage CreateProductImage(ProductImageInputDto dto)
    {
        return new ProductImage
        {
            Id = Guid.NewGuid(),
            Url = dto.Url,
            AltText = dto.AltText,
            DisplayOrder = dto.DisplayOrder
        };
    }

    private static ProductImageDto ToImageDto(ProductImage image)
    {
        return new ProductImageDto
        {
            Id = image.Id,
            Url = image.Url,
            AltText = image.AltText,
            DisplayOrder = image.DisplayOrder
        };
    }

    private async Task<GetProductDto> ToProductDtoAsync(Product product)
    {
        var categoryIds = await _context.ProductCategories
            .AsNoTracking()
            .Where(x => x.ProductId == product.Id)
            .Select(x => x.CategoryId)
            .ToListAsync();

        var tagIds = await _context.ProductTags
            .AsNoTracking()
            .Where(x => x.ProductId == product.Id)
            .Select(x => x.TagId)
            .ToListAsync();

        return new GetProductDto
        {
            Id = product.Id,
            ExternalKey = product.ExternalKey,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            IsPublished = product.IsPublished,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            CategoryIds = categoryIds,
            TagIds = tagIds,
            Images = product.Images.Select(ToImageDto).ToList()
        };
    }
}

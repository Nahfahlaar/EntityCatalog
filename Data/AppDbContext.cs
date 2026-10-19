using System;
using EntityCatalog.Products;
using EntityCatalog.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EntityCatalog.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ProductCategory> ProductCategories { get; set; }
    public DbSet<ProductTag> ProductTags { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("citext");

        // modelBuilder.Entity<Product>().ToTable("Products", table =>
        //     table.HasCheckConstraint("CK_Entities_Price_NonNegative", "\"Price\" >= 0"));
        // modelBuilder.Entity<Category>().ToTable("Categories");
        // modelBuilder.Entity<ProductCategory>().ToTable("ProductCategories");
        // modelBuilder.Entity<ProductTag>().ToTable("ProductTags");
        // modelBuilder.Entity<ProductImage>().ToTable("ProductImages", table =>
        //     table.HasCheckConstraint("CK_ProductImages_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0"));

        modelBuilder.Entity<Product>()
            .Property(e => e.ExternalKey)
            .HasMaxLength(64)
            .IsRequired();

        modelBuilder.Entity<Product>()
            .Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        modelBuilder.Entity<Product>()
            .Property(e => e.Description)
            .HasMaxLength(2000);

        modelBuilder.Entity<Product>()
            .Property(e => e.Price)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        modelBuilder.Entity<Product>()
            .Property(e => e.IsPublished)
            .HasDefaultValue(false)
            .IsRequired();

        modelBuilder.Entity<Product>()
            .HasIndex(e => e.ExternalKey)
            .HasDatabaseName("IX_Entities_ExternalKey")
            .IsUnique();

        modelBuilder.Entity<Category>()
            .Property(g => g.Name)
            .HasMaxLength(100)
            .IsRequired()
            .HasColumnType("citext");

        modelBuilder.Entity<Category>()
            .HasIndex(g => g.Name)
            .HasDatabaseName("IX_Groups_Name")
            .IsUnique();

        modelBuilder.Entity<Tag>()
            .Property(t => t.Name)
            .HasMaxLength(50)
            .IsRequired()
            .HasColumnType("citext");

        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .HasDatabaseName("IX_Tags_Name")
            .IsUnique();

        modelBuilder.Entity<ProductCategory>()
            .HasKey(x => new { x.ProductId, x.CategoryId });

        modelBuilder.Entity<ProductCategory>()
            .HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId);

        modelBuilder.Entity<ProductCategory>()
            .HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductTag>()
            .HasKey(x => new { x.ProductId, x.TagId });

        modelBuilder.Entity<ProductTag>()
            .HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId);

        modelBuilder.Entity<ProductTag>()
            .HasOne<Tag>()
            .WithMany()
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductImage>()
            .Property(x => x.Url)
            .HasMaxLength(2048)
            .IsRequired();

        modelBuilder.Entity<ProductImage>()
            .Property(x => x.AltText)
            .HasMaxLength(500);

        modelBuilder.Entity<Product>()
            .HasMany(x => x.Images)
            .WithOne()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Product>()
            .Property(e => e.CreatedAt)
            .HasColumnType("timestamptz");

        modelBuilder.Entity<Product>()
            .Property(e => e.UpdatedAt)
            .HasColumnType("timestamptz");
    }
}

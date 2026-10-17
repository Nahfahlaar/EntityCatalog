using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EntityCatalog.Data;
using EntityCatalog.Dtos;
using EntityCatalog.Products;
using EntityCatalog.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace EntityCatalog.Tests;

public sealed class PostgreSqlIntegrationTests
{
    private const string InitialMigration = "20260920174454_InitialCreate";
    private const string SigningKey = "integration-test-key-with-at-least-32-bytes";

    [Fact]
    public async Task MigrationsCreateEmptySchemaAndCitextConstraints()
    {
        await using var pg = await PostgreSqlTestHost.StartAsync();
        await using var db = pg.CreateDbContext();

        await db.Database.MigrateAsync();

        Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.True(await db.Database.SqlQueryRaw<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'citext') AS \"Value\"").SingleAsync());
        Assert.True(await db.Database.SqlQueryRaw<bool>(
            "SELECT to_regclass(format('%I.%I', current_schema(), 'AspNetUsers')) IS NOT NULL AS \"Value\"").SingleAsync());

        db.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Books" });
        await db.SaveChangesAsync();
        db.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "books" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await using var constraintDb = pg.CreateDbContext();
        var productId = Guid.NewGuid();
        constraintDb.Products.Add(NewProduct(productId, "unique-external-key"));
        await constraintDb.SaveChangesAsync();
        await using var duplicateProductDb = pg.CreateDbContext();
        duplicateProductDb.Products.Add(NewProduct(Guid.NewGuid(), "unique-external-key"));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateProductDb.SaveChangesAsync());

        await using var tagDb = pg.CreateDbContext();
        tagDb.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "Unique tag" });
        await tagDb.SaveChangesAsync();
        await using var duplicateTagDb = pg.CreateDbContext();
        duplicateTagDb.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "UNIQUE TAG" });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateTagDb.SaveChangesAsync());
    }

    [Fact]
    public async Task MigrationsUpgradeInitialCatalogAndPreserveExistingProducts()
    {
        await using var pg = await PostgreSqlTestHost.StartAsync();
        await using var db = pg.CreateDbContext();
        await db.Database.MigrateAsync(InitialMigration);

        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Entities" ("Id", "ExternalKey", "Name", "Description", "CreatedAt", "UpdatedAt")
            VALUES ({id}, {"existing-legacy-product"}, {"Legacy product"}, {null}, {DateTime.UtcNow}, {DateTime.UtcNow})
            """);

        await db.Database.MigrateAsync();
        var product = await db.Products.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(0m, product.Price);
        Assert.False(product.IsPublished);
        Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task IdentityLoginIssuesEditorJwtAndCatalogEnforcesVisibilityAndConstraints()
    {
        await using var pg = await PostgreSqlTestHost.StartAsync();
        using var app = new PostgreSqlApiHost(pg.ConnectionString);
        using var client = app.CreateClient();
        await using (var db = pg.CreateDbContext())
            await db.Database.MigrateAsync();
        await SeedUsersAsync(app.Services);

        var badLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "editor-integration", password = "Incorrect-Password1!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "editor-integration", password = "Editor-Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
        var editor = app.CreateClient();
        editor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoryResponse = await editor.PostAsJsonAsync("/api/categories", new { name = "Integration" });
        Assert.Equal(HttpStatusCode.OK, categoryResponse.StatusCode);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<GetCategoryDto>())!;
        var tagResponse = await editor.PostAsJsonAsync("/api/tags", new { name = "Integration tag" });
        Assert.Equal(HttpStatusCode.OK, tagResponse.StatusCode);
        var tag = (await tagResponse.Content.ReadFromJsonAsync<GetTagDto>())!;

        var productResponse = await editor.PostAsJsonAsync("/api/products", new
        {
            externalKey = "pg-integration-product", name = "Private item", description = (string?)null,
            price = 10m, categoryIds = new[] { category.Id }, tagIds = new[] { tag.Id },
            images = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);
        var product = (await productResponse.Content.ReadFromJsonAsync<GetProductDto>())!;
        Assert.False(product.IsPublished);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/products/{product.Id}")).StatusCode);
        var publicList = await client.GetFromJsonAsync<List<GetProductDto>>("/api/products");
        Assert.DoesNotContain(publicList!, p => p.Id == product.Id);
        var editorView = await editor.GetFromJsonAsync<GetProductDto>($"/api/products/{product.Id}");
        Assert.Equal(product.Id, editorView!.Id);

        using var member = app.CreateClient();
        var memberLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "member-integration", password = "Member-Password123!"
        });
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await memberLogin.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/categories", new { name = "Forbidden" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await editor.PutAsJsonAsync($"/api/products/{product.Id}/publication", new { isPublished = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/products/{product.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict,
            (await editor.PostAsJsonAsync("/api/categories", new { name = "integration" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await editor.PostAsJsonAsync("/api/tags", new { name = "INTEGRATION TAG" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await editor.PostAsJsonAsync("/api/products", new
            {
                externalKey = "pg-integration-product", name = "Duplicate", description = (string?)null,
                price = 1m, categoryIds = new[] { category.Id }, tagIds = Array.Empty<Guid>(), images = Array.Empty<object>()
            })).StatusCode);

        await using var directDb = pg.CreateDbContext();
        directDb.Categories.Remove(await directDb.Categories.SingleAsync(x => x.Id == category.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => directDb.SaveChangesAsync());
        await using var tagDb = pg.CreateDbContext();
        tagDb.Tags.Remove(await tagDb.Tags.SingleAsync(x => x.Id == tag.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => tagDb.SaveChangesAsync());
    }

    private static async Task SeedUsersAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Editor" };
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        var editor = new ApplicationUser { Id = Guid.NewGuid(), UserName = "editor-integration", LockoutEnabled = true };
        Assert.True((await userManager.CreateAsync(editor, "Editor-Password123!")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(editor, "Editor")).Succeeded);
        var member = new ApplicationUser { Id = Guid.NewGuid(), UserName = "member-integration", LockoutEnabled = true };
        Assert.True((await userManager.CreateAsync(member, "Member-Password123!")).Succeeded);
    }

    private static Product NewProduct(Guid id, string externalKey)
    {
        var now = DateTime.UtcNow;
        return new Product
        {
            Id = id,
            ExternalKey = externalKey,
            Name = "Constraint test product",
            Price = 1m,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed class PostgreSqlApiHost(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Jwt:Issuer"] = "EntityCatalog.IntegrationTests",
                    ["Jwt:Audience"] = "EntityCatalog.IntegrationTests.Client",
                    ["Jwt:SigningKey"] = SigningKey
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }

    private sealed class PostgreSqlTestHost : IAsyncDisposable
    {
        private const string ConnectionStringEnvironmentVariable = "ENTITYCATALOG_TEST_POSTGRES";
        private const string DatabasePrefix = "entitycatalog_test";
        private readonly string _adminConnectionString;
        private readonly string _schema;
        public string ConnectionString { get; }

        private PostgreSqlTestHost(string adminConnectionString, string schema)
        {
            _adminConnectionString = adminConnectionString;
            _schema = schema;
            var testConnection = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                SearchPath = $"{schema},public"
            };
            ConnectionString = testConnection.ConnectionString;
        }

        public static async Task<PostgreSqlTestHost> StartAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    $"Set {ConnectionStringEnvironmentVariable} to a dedicated PostgreSQL test database connection string.");

            var parsed = new NpgsqlConnectionStringBuilder(connectionString);
            var databaseName = parsed.Database;
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.StartsWith(DatabasePrefix, StringComparison.OrdinalIgnoreCase) ||
                (databaseName.Length > DatabasePrefix.Length &&
                 databaseName[DatabasePrefix.Length] != '_'))
            {
                throw new InvalidOperationException(
                    $"Refusing to run PostgreSQL integration tests against database '{databaseName}'. " +
                    $"The database name must be '{DatabasePrefix}' or start with '{DatabasePrefix}_'.");
            }

            var schema = $"ectest_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection))
                await command.ExecuteNonQueryAsync();

            return new PostgreSqlTestHost(connectionString, schema);
        }

        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

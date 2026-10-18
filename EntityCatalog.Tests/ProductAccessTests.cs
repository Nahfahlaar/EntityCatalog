using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using EntityCatalog.Data;
using EntityCatalog.Dtos;
using EntityCatalog.Products;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EntityCatalog.Tests;

public class ProductAccessTests
{
    [Fact]
    public async Task ProductListRejectsPageValuesThatWouldOverflowSkip()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        var response = await client.GetAsync("/api/products?page=2147483647&pageSize=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousAndNonEditorReadsOnlyPublishedProductsAndRelatedData()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        var seed = await host.SeedAsync();

        var list = await client.GetFromJsonAsync<List<GetProductDto>>("/api/products");
        Assert.NotNull(list);
        Assert.Equal([seed.PublishedProductId], list.Select(product => product.Id));

        Assert.Empty((await client.GetFromJsonAsync<List<GetProductDto>>("/api/products?search=Hidden"))!);
        Assert.Empty((await client.GetFromJsonAsync<List<GetProductDto>>($"/api/products?categoryId={seed.CategoryId}&search=Hidden"))!);
        Assert.Empty((await client.GetFromJsonAsync<List<GetProductDto>>($"/api/products?tagId={seed.TagId}&search=Hidden"))!);
        var dateAndPageFiltered = await client.GetFromJsonAsync<List<GetProductDto>>(
            "/api/products?createdFrom=2000-01-01&createdTo=2099-01-01&sort=createdAt&order=desc&pageSize=1");
        Assert.Equal([seed.PublishedProductId], dateAndPageFiltered!.Select(product => product.Id));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/products/{seed.UnpublishedProductId}")).StatusCode);

        var categories = await client.GetFromJsonAsync<List<GetCategoryDto>>("/api/categories");
        var tags = await client.GetFromJsonAsync<List<GetTagDto>>("/api/tags");
        Assert.Contains(categories!, category => category.Id == seed.CategoryId);
        Assert.Contains(tags!, tag => tag.Id == seed.TagId);

        using var member = host.CreateAuthenticatedClient("Member");
        var memberList = await member.GetFromJsonAsync<List<GetProductDto>>("/api/products");
        Assert.Equal([seed.PublishedProductId], memberList!.Select(product => product.Id));
        Assert.Equal(HttpStatusCode.NotFound,
            (await member.GetAsync($"/api/products/{seed.UnpublishedProductId}")).StatusCode);
    }

    [Fact]
    public async Task EditorCanSeeAllProductsAndCanPublishAndUnpublish()
    {
        using var host = new ApiTestHost();
        using var publicClient = host.CreateClient();
        using var editor = host.CreateAuthenticatedClient("Editor");
        var seed = await host.SeedAsync();

        var editorList = await editor.GetFromJsonAsync<List<GetProductDto>>("/api/products");
        Assert.Contains(editorList!, product => product.Id == seed.UnpublishedProductId);

        var hidden = await editor.GetFromJsonAsync<GetProductDto>(
            $"/api/products/{seed.UnpublishedProductId}");
        Assert.NotNull(hidden);
        Assert.Contains(seed.CategoryId, hidden.CategoryIds);
        Assert.Contains(seed.TagId, hidden.TagIds);
        Assert.Contains(hidden.Images, image => image.Url == "https://example.test/hidden.jpg");

        var publicationUrl = $"/api/products/{seed.UnpublishedProductId}/publication";
        Assert.Equal(HttpStatusCode.NoContent,
            (await editor.PutAsJsonAsync(publicationUrl, new { isPublished = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await publicClient.GetAsync($"/api/products/{seed.UnpublishedProductId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await editor.PutAsJsonAsync(publicationUrl, new { isPublished = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await publicClient.GetAsync($"/api/products/{seed.UnpublishedProductId}")).StatusCode);
    }

    [Fact]
    public async Task OnlyEditorCanChangeProductsCategoriesAndTags()
    {
        using var host = new ApiTestHost();
        using var anonymous = host.CreateClient();
        using var member = host.CreateAuthenticatedClient("Member");
        using var editor = host.CreateAuthenticatedClient("Editor");
        var seed = await host.SeedAsync();

        var productBody = new
        {
            externalKey = "new-product",
            name = "New product",
            description = (string?)null,
            price = 12.50m,
            categoryIds = new[] { seed.CategoryId },
            tagIds = new[] { seed.TagId },
            images = Array.Empty<object>()
        };

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/products", productBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/products", productBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/products/{seed.PublishedProductId}", productBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PutAsJsonAsync($"/api/products/{seed.PublishedProductId}", productBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/categories", new { name = "Public attempt" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/categories", new { name = "Member attempt" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/tags", new { name = "Public attempt" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/tags", new { name = "Member attempt" })).StatusCode);

        var createdCategory = await editor.PostAsJsonAsync("/api/categories", new { name = "Editor category" });
        var createdTag = await editor.PostAsJsonAsync("/api/tags", new { name = "Editor tag" });
        Assert.Equal(HttpStatusCode.OK, createdCategory.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createdTag.StatusCode);

        var createdResponse = await editor.PostAsJsonAsync("/api/products", productBody);
        Assert.True(createdResponse.StatusCode == HttpStatusCode.OK,
            $"Expected product create to succeed, got {(int)createdResponse.StatusCode}: {await createdResponse.Content.ReadAsStringAsync()}");
        var createdProduct = await createdResponse.Content.ReadFromJsonAsync<GetProductDto>();
        Assert.NotNull(createdProduct);
        Assert.False(createdProduct.IsPublished);

        Assert.Equal(HttpStatusCode.OK,
            (await editor.PutAsJsonAsync($"/api/products/{createdProduct.Id}",
                new
                {
                    externalKey = "new-product",
                    name = "Updated product",
                    description = (string?)null,
                    price = 15m,
                    categoryIds = new[] { seed.CategoryId },
                    tagIds = Array.Empty<Guid>(),
                    images = Array.Empty<object>()
                })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync($"/api/products/{seed.PublishedProductId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.DeleteAsync($"/api/products/{seed.PublishedProductId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await editor.DeleteAsync($"/api/products/{createdProduct.Id}")).StatusCode);
    }

    [Fact]
    public async Task PublicationEndpointRequiresEditorRole()
    {
        using var host = new ApiTestHost();
        using var anonymous = host.CreateClient();
        using var member = host.CreateAuthenticatedClient("Member");
        using var editor = host.CreateAuthenticatedClient("Editor");
        var seed = await host.SeedAsync();
        var url = $"/api/products/{seed.PublishedProductId}/publication";
        var body = new { isPublished = false };

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PutAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await editor.PatchAsync(url, JsonContent.Create(body))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await editor.PostAsJsonAsync(url, body)).StatusCode);
    }

    [Fact]
    public async Task CategoryAndTagUpdateAndDeleteAreEditorOnlyAndPreserveLinks()
    {
        using var host = new ApiTestHost();
        using var anonymous = host.CreateClient();
        using var member = host.CreateAuthenticatedClient("Member");
        using var editor = host.CreateAuthenticatedClient("Editor");
        var seed = await host.SeedAsync();

        foreach (var path in new[] { "categories", "tags" })
        {
            var body = new { name = $"Renamed {path}" };
            var url = $"/api/{path}/{(path == "categories" ? seed.CategoryId : seed.TagId)}";

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymous.PutAsJsonAsync(url, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await member.PutAsJsonAsync(url, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymous.DeleteAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await member.DeleteAsync(url)).StatusCode);

            Assert.Equal(HttpStatusCode.OK,
                (await editor.PutAsJsonAsync(url, body)).StatusCode);
            var deleteInUse = await editor.DeleteAsync(url);
            Assert.Equal(HttpStatusCode.Conflict, deleteInUse.StatusCode);
            Assert.Equal("application/problem+json", deleteInUse.Content.Headers.ContentType?.MediaType);
        }

        var product = await editor.GetFromJsonAsync<GetProductDto>(
            $"/api/products/{seed.UnpublishedProductId}");
        Assert.Contains(seed.CategoryId, product!.CategoryIds);
        Assert.Contains(seed.TagId, product.TagIds);

        var unusedCategory = await editor.PostAsJsonAsync("/api/categories", new { name = "Unused" });
        var unusedCategoryData = await unusedCategory.Content.ReadFromJsonAsync<GetCategoryDto>();
        Assert.Equal(HttpStatusCode.NoContent,
            (await editor.DeleteAsync($"/api/categories/{unusedCategoryData!.Id}")).StatusCode);

        var unusedTag = await editor.PostAsJsonAsync("/api/tags", new { name = "Unused" });
        var unusedTagData = await unusedTag.Content.ReadFromJsonAsync<GetTagDto>();
        Assert.Equal(HttpStatusCode.NoContent,
            (await editor.DeleteAsync($"/api/tags/{unusedTagData!.Id}")).StatusCode);
    }

    [Fact]
    public async Task MissingMalformedExpiredAndInvalidSignatureTokensCannotWrite()
    {
        using var host = new ApiTestHost();
        var seed = await host.SeedAsync();
        var productBody = new
        {
            externalKey = "unauthorized-product",
            name = "Unauthorized product",
            description = (string?)null,
            price = 1m,
            categoryIds = new[] { seed.CategoryId },
            tagIds = Array.Empty<Guid>(),
            images = Array.Empty<object>()
        };

        using var anonymous = host.CreateClient();
        using var malformed = host.CreateClientWithToken("not-a-jwt");
        using var expired = host.CreateClientWithToken(host.CreateToken("Editor", expired: true));
        using var badSignature = host.CreateClientWithToken(host.CreateToken("Editor", invalidSignature: true));

        foreach (var client in new[] { anonymous, malformed, expired, badSignature })
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/products", productBody)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PutAsJsonAsync($"/api/products/{seed.PublishedProductId}/publication",
                    new { isPublished = false })).StatusCode);
        }
    }

    private sealed class ApiTestHost : WebApplicationFactory<Program>
    {
        private const string Issuer = "EntityCatalog.Tests";
        private const string Audience = "EntityCatalog.Tests.Client";
        private readonly string _signingKey = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public ApiTestHost()
        {
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = _signingKey
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            });
        }

        public HttpClient CreateAuthenticatedClient(string role)
        {
            return CreateClientWithToken(CreateToken(role));
        }

        public HttpClient CreateClientWithToken(string token)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", token);
            return client;
        }

        public async Task<SeedData> SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var category = new Category { Id = Guid.NewGuid(), Name = "Games" };
            var tag = new Tag { Id = Guid.NewGuid(), Name = "RPG" };
            var published = MakeProduct("Published game", true);
            var hidden = MakeProduct("Hidden game", false);
            hidden.CreatedAt = hidden.UpdatedAt = DateTime.UtcNow.AddMinutes(1);
            db.Categories.Add(category);
            db.Tags.Add(tag);
            db.Products.AddRange(published, hidden);
            db.ProductCategories.AddRange(
                new ProductCategory { ProductId = published.Id, CategoryId = category.Id },
                new ProductCategory { ProductId = hidden.Id, CategoryId = category.Id });
            db.ProductTags.AddRange(
                new ProductTag { ProductId = published.Id, TagId = tag.Id },
                new ProductTag { ProductId = hidden.Id, TagId = tag.Id });
            db.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = hidden.Id,
                Url = "https://example.test/hidden.jpg",
                DisplayOrder = 0
            });
            await db.SaveChangesAsync();
            return new SeedData(category.Id, tag.Id, published.Id, hidden.Id);
        }

        public string CreateToken(string role, bool expired = false, bool invalidSignature = false)
        {
            var signingKey = invalidSignature ? new string('x', 48) : _signingKey;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: [
                    new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                    new Claim("role", role)
                ],
                notBefore: expired ? now.AddMinutes(-10) : now.AddMinutes(-1),
                expires: expired ? now.AddMinutes(-5) : now.AddMinutes(5),
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static Product MakeProduct(string name, bool isPublished)
        {
            var now = DateTime.UtcNow;
            return new Product
            {
                Id = Guid.NewGuid(),
                ExternalKey = Guid.NewGuid().ToString("N"),
                Name = name,
                Price = 10m,
                IsPublished = isPublished,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _connection.Dispose();
        }
    }

    private sealed record SeedData(Guid CategoryId, Guid TagId, Guid PublishedProductId, Guid UnpublishedProductId);
}

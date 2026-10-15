using FluentValidation;
using FluentValidation.Results;
using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Catalog;

public sealed class AdminCategoryService(AppDbContext db, TimeProvider timeProvider)
{
    private DbSet<Category> Categories => db.Set<Category>();

    public async Task<IReadOnlyList<AdminCategoryResponse>> ListAsync(CancellationToken ct) =>
        await Categories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new AdminCategoryResponse(
                c.Id, c.Name, c.Slug, c.Icon, c.Description, c.SortOrder, c.IsActive,
                db.Set<Product>().Count(p => p.CategoryId == c.Id)))
            .ToListAsync(ct);

    public async Task<AdminCategoryResponse> CreateAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, exceptId: null, ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var category = new Category { Slug = await UniqueSlugAsync(name, ct), CreatedAtUtc = now };
        Apply(request, category, now);
        Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return ToResponse(category, productCount: 0);
    }

    public async Task<AdminCategoryResponse> UpdateAsync(int id, SaveCategoryRequest request, CancellationToken ct)
    {
        var category = await FindAsync(id, ct);
        await EnsureNameIsFreeAsync(request.Name.Trim(), exceptId: id, ct);

        Apply(request, category, timeProvider.GetUtcNow().UtcDateTime); // the slug never changes
        await db.SaveChangesAsync(ct);

        return ToResponse(category, await db.Set<Product>().CountAsync(p => p.CategoryId == id, ct));
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var category = await FindAsync(id, ct);
        var productCount = await db.Set<Product>().CountAsync(p => p.CategoryId == id, ct);
        if (productCount > 0)
        {
            throw new ConflictException(
                $"This category still has {productCount} product(s). Move them to another category, or hide the category instead.");
        }

        Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Category> FindAsync(int id, CancellationToken ct) =>
        await Categories.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Category not found.");

    private async Task EnsureNameIsFreeAsync(string name, int? exceptId, CancellationToken ct)
    {
        if (await Categories.AnyAsync(c => c.Name == name && c.Id != exceptId, ct))
        {
            throw new ValidationException([new ValidationFailure("name", "A category with this name already exists.")]);
        }
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugs.From(name) is { Length: > 0 } s ? s : "category";
        var slug = baseSlug;
        for (var n = 2; await Categories.AnyAsync(c => c.Slug == slug, ct); n++)
        {
            slug = $"{baseSlug}-{n}";
        }

        return slug;
    }

    private static void Apply(SaveCategoryRequest request, Category category, DateTime now)
    {
        category.Name = request.Name.Trim();
        category.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = now;
    }

    private static AdminCategoryResponse ToResponse(Category c, int productCount) =>
        new(c.Id, c.Name, c.Slug, c.Icon, c.Description, c.SortOrder, c.IsActive, productCount);
}

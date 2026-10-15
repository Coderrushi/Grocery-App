using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Common.Security;
using GroceryStore.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Catalog;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class AdminCategoriesController(AdminCategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AdminCategoryResponse>> List(CancellationToken ct) => categories.ListAsync(ct);

    [HttpPost]
    [ProducesResponseType<AdminCategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminCategoryResponse>> Create(SaveCategoryRequest request, CancellationToken ct)
    {
        var category = await categories.CreateAsync(request, ct);
        return CreatedAtAction(nameof(List), category);
    }

    [HttpPut("{id:int}")]
    public Task<AdminCategoryResponse> Update(int id, SaveCategoryRequest request, CancellationToken ct) =>
        categories.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await categories.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class AdminProductsController(AdminProductService products, InventoryService inventory) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminProductResponse>> List([FromQuery] AdminProductQuery query, CancellationToken ct) =>
        products.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AdminProductResponse> Get(int id, CancellationToken ct) => products.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminProductResponse>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await products.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    /// <summary>Updates product details. Returns 409 if someone else saved the product since it was loaded.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AdminProductResponse> Update(int id, UpdateProductRequest request, CancellationToken ct) =>
        products.UpdateAsync(id, request, ct);

    /// <summary>Uploads or replaces the product photo (JPEG, PNG or WebP, up to 2 MB).</summary>
    [HttpPost("{id:int}/image")]
    [RequireXhrHeader]
    [RequestSizeLimit(ImageFile.MaxBytes + 64 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<AdminProductResponse> UploadImage(int id, IFormFile file, CancellationToken ct) =>
        products.SetImageAsync(id, file, ct);

    [HttpDelete("{id:int}/image")]
    public Task<AdminProductResponse> RemoveImage(int id, CancellationToken ct) => products.RemoveImageAsync(id, ct);

    /// <summary>Adds or removes stock with a reason; recorded in the stock history.</summary>
    [HttpPost("{id:int}/stock-adjustments")]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<AdminProductResponse> AdjustStock(int id, AdjustStockRequest request, CancellationToken ct)
    {
        await inventory.AdjustAsync(id, request.QuantityChange, request.Reason, request.Note, User.GetUserId(), ct);
        return await products.GetAsync(id, ct);
    }

    [HttpGet("{id:int}/stock-adjustments")]
    public Task<IReadOnlyList<InventoryAdjustmentResponse>> StockHistory(int id, CancellationToken ct) =>
        inventory.HistoryAsync(id, take: 50, ct);
}

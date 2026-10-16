using GroceryStore.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Catalog;

/// <summary>Public storefront catalogue. No sign-in required.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/catalog")]
[Produces("application/json")]
public sealed class CatalogController(CatalogQueryService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryResponse>> Categories(CancellationToken ct) => catalog.GetCategoriesAsync(ct);

    /// <summary>Browse, filter, search and sort products. All parameters are optional.</summary>
    [HttpGet("products")]
    public Task<PagedResult<ProductCardResponse>> Products([FromQuery] ProductQuery query, CancellationToken ct) =>
        catalog.SearchProductsAsync(query, ct);

    [HttpGet("products/{id:int}")]
    [ProducesResponseType<ProductDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProductDetailResponse> Product(int id, CancellationToken ct) => catalog.GetProductAsync(id, ct);
}

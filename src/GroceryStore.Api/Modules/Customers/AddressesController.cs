using GroceryStore.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Customers;

[ApiController]
[Route("api/account/addresses")]
[Authorize]
[Produces("application/json")]
public sealed class AddressesController(AddressService addresses) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AddressResponse>> List(CancellationToken ct) =>
        addresses.ListAsync(User.GetUserId(), ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AddressResponse> Get(int id, CancellationToken ct) =>
        addresses.GetAsync(User.GetUserId(), id, ct);

    [HttpPost]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddressResponse>> Create(SaveAddressRequest request, CancellationToken ct)
    {
        var address = await addresses.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = address.Id }, address);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AddressResponse> Update(int id, SaveAddressRequest request, CancellationToken ct) =>
        addresses.UpdateAsync(User.GetUserId(), id, request, ct);

    [HttpPost("{id:int}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        await addresses.SetDefaultAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await addresses.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}

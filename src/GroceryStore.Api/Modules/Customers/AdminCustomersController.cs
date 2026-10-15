using GroceryStore.Api.Common;
using GroceryStore.Api.Data;
using GroceryStore.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Customers;

/// <summary>Shopkeeper view of registered customers.</summary>
[ApiController]
[Route("api/admin/customers")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class AdminCustomersController(AppDbContext db) : ControllerBase
{
    /// <summary>Customers, newest first. <paramref name="search"/> matches name, email or mobile number.</summary>
    [HttpGet]
    public Task<PagedResult<CustomerSummaryResponse>> List(
        [FromQuery] string? search, [FromQuery] PageRequest paging, CancellationToken ct)
    {
        var customerRoleId = db.Roles.Where(r => r.Name == Roles.Customer).Select(r => r.Id);
        var customers = db.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && customerRoleId.Contains(ur.RoleId)));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            customers = customers.Where(u =>
                u.FullName.Contains(term) || u.Email!.Contains(term) || u.PhoneNumber!.Contains(term));
        }

        return customers
            .OrderByDescending(u => u.CreatedAtUtc).ThenByDescending(u => u.Id)
            .Select(u => new CustomerSummaryResponse(
                u.Id, u.FullName, u.Email!, u.PhoneNumber, u.CreatedAtUtc,
                db.Set<Address>().Count(a => a.UserId == u.Id)))
            .ToPagedResultAsync(paging, ct);
    }
}

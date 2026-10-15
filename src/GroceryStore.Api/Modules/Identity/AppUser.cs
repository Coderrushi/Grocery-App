using Microsoft.AspNetCore.Identity;

namespace GroceryStore.Api.Modules.Identity;

/// <summary>
/// An application user (customer or shop staff). Uses an int key: compact, index-friendly in
/// MySQL/InnoDB, and avoids the provider's Guid mapping quirks. Profile fields arrive in Phase 2.
/// </summary>
public class AppUser : IdentityUser<int>
{
    public required string FullName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AppRole : IdentityRole<int>
{
}

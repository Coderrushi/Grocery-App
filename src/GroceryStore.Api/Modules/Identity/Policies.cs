namespace GroceryStore.Api.Modules.Identity;

/// <summary>
/// Authorization policy names. Endpoints ask for a capability ("manage the store"), not a role, so a
/// future Staff role can be granted some capabilities without touching every controller.
/// Every endpoint requires a signed-in user unless it is marked [AllowAnonymous] (the fallback policy).
/// </summary>
public static class Policies
{
    /// <summary>The back office: catalogue, orders, customers, wallets and reports. Admin role today.</summary>
    public const string ManageStore = "ManageStore";
}

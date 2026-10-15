namespace GroceryStore.Api.Modules.Wallet;

public sealed record PaymentResult(bool Succeeded, string? ProviderReference, string? FailureReason);

/// <summary>
/// The boundary to a payment provider. Today: <see cref="SimulatedPaymentGateway"/>.
/// A real provider (Razorpay, PayU, Cashfree…) is asynchronous: the API creates an order with the
/// provider, the customer pays on the provider's page, and a signed webhook reports the outcome. At that
/// point "charge" splits into "create" + "confirm (webhook)", and only this interface, its implementation
/// and TopUpService's confirm step change. The wallet ledger is untouched. See ADR 0003.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }

    Task<PaymentResult> ChargeAsync(long topUpId, decimal amount, int userId, CancellationToken ct);

    /// <summary>Gives money back when a charge succeeded but could not be credited.</summary>
    Task RefundAsync(string providerReference, decimal amount, CancellationToken ct);
}

/// <summary>Development stand-in: every payment succeeds immediately. No real money moves.</summary>
internal sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public string Name => "Simulated";

    public Task<PaymentResult> ChargeAsync(long topUpId, decimal amount, int userId, CancellationToken ct) =>
        Task.FromResult(new PaymentResult(true, $"SIM-{topUpId}-{Guid.NewGuid():N}"[..32], null));

    public Task RefundAsync(string providerReference, decimal amount, CancellationToken ct) => Task.CompletedTask;
}

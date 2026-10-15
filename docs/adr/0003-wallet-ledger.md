# ADR 0003: Store wallet as a ledger, with a payment-gateway seam

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

Customers pay for orders from a wallet that they top up. Payments are **simulated** for now, but
real top-ups through an Indian gateway (Razorpay, PayU, Cashfree) must be possible later without
rewriting how balances work. Order payments and refunds (Phase 6) will also move wallet money.

## Decision

1. **Ledger first.** `wallet_transactions` is append-only and records every credit (+) and debit (−)
   with `BalanceAfter`. `wallets.Balance` is a cached running total, updated in the **same
   transaction** as each ledger insert. A test asserts `Balance = SUM(Amount)` for every wallet.
2. **One gateway for money: `WalletService`.**
   - A debit is one conditional `UPDATE … WHERE Balance >= amount`, so it can never overdraw, even
     under concurrent requests. A DB CHECK (`Balance >= 0`) is the last line of defence.
   - The maximum balance is enforced the same way for top-ups.
3. **Exactly-once events.**
   - Ledger rows created by a business event carry a unique `Reference`: `topup:{id}` now,
     `order:{id}:payment` and `order:{id}:refund` in Phase 6. The database rejects a second credit
     or debit for the same event.
   - Customer top-ups also carry a client idempotency key, unique per user, so retries and double
     taps return the first result.
4. **Top-ups are a state machine** (`Pending → Succeeded | Failed`) in `wallet_top_ups`, and go
   through `IPaymentGateway`. Only a Succeeded top-up credits the wallet.
   - The limit is checked before charging.
   - If the charge succeeds but crediting fails (the limit was reached concurrently), the gateway
     refund is called and the top-up is marked Failed. Money is never kept without being credited.
5. **Closed system.** The wallet can only be spent at this store, and there is no withdrawal to a
   bank. Manual admin credits and debits require a reason and note, and record the admin's id.

## Moving to a real gateway

The simulated gateway charges synchronously. A real one is asynchronous, so the work is:

1. `POST /api/wallet/top-ups` creates the Pending top-up and a **gateway order**, and returns the
   gateway's checkout details to the browser.
2. The customer pays on the gateway's checkout page.
3. A **webhook endpoint** verifies the gateway signature and calls the existing "credit and mark
   Succeeded" step. That step is idempotent through the unique `topup:{id}` reference, so duplicate
   webhooks are harmless.
4. A periodic job reconciles Pending top-ups older than N minutes with the gateway.

The ledger, `WalletService` and every balance rule stay unchanged.

## Regulatory note (India)

A stored-value wallet is a Prepaid Payment Instrument. RBI rules generally allow a **closed-system**
PPI (usable only for the issuer's own goods, no cash-out) without RBI authorisation. That is why there
is no withdrawal feature. **Confirm with a chartered accountant or lawyer before accepting real money**,
including GST treatment and KYC expectations.

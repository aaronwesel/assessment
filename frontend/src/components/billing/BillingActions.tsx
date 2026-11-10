/**
 * Checkout + Customer Portal actions for the billing settings page.
 * Redirects to Stripe-hosted URLs; fulfillment is webhook-driven on the API.
 */
type Props = {
  busy: boolean
  canCheckout: boolean
  canPortal: boolean
  onCheckout: () => void
  onPortal: () => void
}

export function BillingActions({
  busy,
  canCheckout,
  canPortal,
  onCheckout,
  onPortal,
}: Props) {
  return (
    <div className="billing-actions">
      <button
        type="button"
        className="btn btn-primary"
        disabled={busy || !canCheckout}
        onClick={onCheckout}
      >
        Subscribe with Stripe Checkout
      </button>
      <button
        type="button"
        className="btn btn-ghost"
        disabled={busy || !canPortal}
        onClick={onPortal}
      >
        Open Customer Portal
      </button>
    </div>
  )
}

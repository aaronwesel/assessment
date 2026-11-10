/**
 * Renders a scannable TOTP QR from an otpauth:// provisioning URI.
 */
import { useEffect, useState } from 'react'
import QRCode from 'qrcode'

type Props = {
  otpauthUri: string
  alt?: string
}

export function TotpQrCode({ otpauthUri, alt = 'Scan to add authenticator' }: Props) {
  const [dataUrl, setDataUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setDataUrl(null)
    setError(null)
    void QRCode.toDataURL(otpauthUri, { width: 220, margin: 2, errorCorrectionLevel: 'M' })
      .then((url) => {
        if (!cancelled) setDataUrl(url)
      })
      .catch(() => {
        if (!cancelled) setError('Could not render QR code.')
      })
    return () => {
      cancelled = true
    }
  }, [otpauthUri])

  if (error) {
    return <p className="form-error">{error}</p>
  }
  if (!dataUrl) {
    return <p className="hint">Preparing QR code…</p>
  }

  return (
    <img
      className="mfa-qr"
      src={dataUrl}
      width={220}
      height={220}
      alt={alt}
    />
  )
}

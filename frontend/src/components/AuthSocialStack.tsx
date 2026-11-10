/**
 * Shared Google One Tap + Microsoft sign-in block for login and register pages.
 */
import { GoogleOneTap } from './GoogleOneTap'
import { MicrosoftSignIn } from './MicrosoftSignIn'

type Props = {
  dividerLabel?: string
}

export function AuthSocialStack({ dividerLabel = 'or' }: Props) {
  return (
    <>
      <GoogleOneTap />
      <MicrosoftSignIn />
      <div className="divider" role="separator">
        <span>{dividerLabel}</span>
      </div>
    </>
  )
}

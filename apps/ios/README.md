# Assessment iOS app (scaffold)

SwiftUI stub that shares the FastAPI backend with the web and Android clients.

**Not App Store ready** — login and API wiring are placeholders only. Requires a Mac with Xcode 15+.

## Open in Xcode

**Option A — XcodeGen (recommended)**

1. Install [XcodeGen](https://github.com/yonaskolb/XcodeGen): `brew install xcodegen`
2. From this directory: `xcodegen generate`
3. Open `AssessmentApp.xcodeproj`
4. Pick an iOS 17+ simulator and Run

**Option B — manual**

1. Xcode → **File → New → Project → App** (SwiftUI, Swift, iOS).
2. Product name `AssessmentApp`, bundle id e.g. `com.assessment.app`.
3. Replace generated sources with the files under `AssessmentApp/`.
4. Ensure `Info.plist` ATS exceptions match local HTTP needs (see below).

## Point at the FastAPI backend

Default base URL is in `AssessmentApp/Config/ApiConfig.swift`:

| Environment | Suggested `baseURL` |
|-------------|---------------------|
| Simulator → host machine Compose/API | `http://127.0.0.1:8000` |
| Physical device → host on LAN | `http://<your-lan-ip>:8000` |
| Staging / ALB | `https://api.example.com` |

Edit `ApiConfig.baseURL` (or set a scheme-specific value later). Keep it aligned with `apps/android` and the web frontend when demos leave localhost.

For local **HTTP** (not HTTPS), `Info.plist` includes a narrow `NSAppTransportSecurity` exception for localhost / LAN scaffolding. Remove or tighten before any real deploy.

## Login stubs (not wired)

`LoginView` shows placeholders for:

- **Google One Tap** — TODO: Google Sign-In / GIS equivalent; exchange ID token with backend
- **Microsoft One Tap** — TODO: MSAL iOS; exchange with backend
- **Email / password** — TODO: POST credentials to FastAPI JWT endpoints

Do not commit OAuth client secrets; use Xcode build settings / CI secrets when apps are registered.

## Push stub (APNs)

`Push/PushConfig.swift` holds APNs team/key/topic placeholders (no `.p8` secrets).  
`Push/AlertReceivedHandler.swift` is the single “alert received” path — call it from a future `UNUserNotificationCenter` delegate (or debug UI) with an opaque alert id. Payloads must never include PHI.

## Package layout

```
AssessmentApp/
  AssessmentAppApp.swift   @main entry
  ContentView.swift        Root host → LoginView
  LoginView.swift          Login placeholders
  Config/ApiConfig.swift   API base URL + auth path stubs
  Push/PushConfig.swift    APNs config placeholders
  Push/AlertReceivedHandler.swift  Alert-received stub path
  Info.plist               ATS localhost exceptions (scaffold)
project.yml                XcodeGen spec
```

## Shared backend contract

Same auth surface as Android/web: JWT (or session) after password or One Tap token exchange. Path stubs in `ApiConfig` should track backend OpenAPI as auth endpoints land.

# Assessment Android app (scaffold)

Kotlin + Jetpack Compose stub that shares the FastAPI backend with the web and iOS clients.

**Not App Store / Play Store ready** — login and API wiring are placeholders only.

## Open in Android Studio

1. Install [Android Studio](https://developer.android.com/studio) (Giraffe+ / AGP 8.x).
2. **File → Open** → select this `apps/android` directory.
3. Let Gradle sync; create an emulator or attach a device (API 26+).
4. Run the `app` configuration.

If the Gradle Wrapper JAR is missing, use Android Studio’s “Use Gradle from: ‘gradle-wrapper.properties’” sync, or run `gradle wrapper` once from this folder.

## Point at the FastAPI backend

Default base URL is set in `app/src/main/java/com/assessment/app/config/ApiConfig.kt`:

| Environment | Suggested `BASE_URL` |
|-------------|----------------------|
| Emulator → host machine Compose/API | `http://10.0.2.2:8000` |
| Physical device → host on LAN | `http://<your-lan-ip>:8000` |
| Staging / ALB | `https://api.example.com` |

Also override at build time with a Gradle property (see `app/build.gradle.kts`):

```bash
./gradlew :app:assembleDebug -PAPI_BASE_URL=http://10.0.2.2:8000
```

Cleartext HTTP to the emulator host is allowed only for debug via `network_security_config.xml`. Use HTTPS for anything beyond local scaffold.

## Login stubs (not wired)

`LoginScreen` shows placeholders for:

- **Google One Tap** — TODO: Credential Manager / GIS; exchange ID token with backend `/auth/...`
- **Microsoft One Tap** — TODO: MSAL; exchange with backend
- **Email / password** — TODO: POST credentials to FastAPI JWT endpoints

Do not ship with real client IDs or secrets in source; inject via `local.properties` or CI secrets when OAuth apps exist.

## Package layout

```
app/src/main/java/com/assessment/app/
  AssessmentApp.kt          Application entry
  MainActivity.kt           Single-activity host
  config/ApiConfig.kt       API base URL
  ui/login/LoginScreen.kt   Login placeholders
  ui/theme/Theme.kt         Minimal Material 3 theme
```

## Shared backend contract

Expect the same auth surface as web/iOS: JWT (or session) after password or One Tap token exchange. Keep `ApiConfig.BASE_URL` aligned with `apps/ios` and the frontend env when demos move off localhost.

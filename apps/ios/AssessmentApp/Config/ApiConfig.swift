import Foundation

/// Shared FastAPI backend base URL.
///
/// Simulator → host: http://127.0.0.1:8000
/// Device → LAN:     http://<lan-ip>:8000
/// Staging:          https://api.example.com
///
/// Keep aligned with apps/android and the web frontend env.
enum ApiConfig {
    static var baseURL: String {
        // Scaffold default for iOS Simulator → host Compose/API.
        let raw = ProcessInfo.processInfo.environment["API_BASE_URL"]
            ?? "http://127.0.0.1:8000"
        return raw.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
    }

    // Auth API paths (same contract as web / Android).
    static let loginPasswordPath = "/api/v1/auth/login"
    static let registerPasswordPath = "/api/v1/auth/register"
    static let loginGooglePath = "/api/v1/auth/google/onetap"
    static let loginMicrosoftPath = "/api/v1/auth/microsoft/onetap"
    static let authMePath = "/api/v1/auth/me"
    static let authRefreshPath = "/api/v1/auth/refresh"
    static let authLogoutPath = "/api/v1/auth/logout"
}

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

    // Placeholder paths — align with backend OpenAPI when auth stubs land.
    static let loginPasswordPath = "/auth/login"
    static let loginGooglePath = "/auth/google"
    static let loginMicrosoftPath = "/auth/microsoft"
}

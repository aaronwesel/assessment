import Foundation

/// APNs / push configuration placeholders.
///
/// Do not commit real APNs keys (.p8) or key IDs in source. Inject via
/// Xcode build settings / CI secrets / Secrets Manager for staging.
///
/// HIPAA: push payloads must never include PHI — use opaque alert IDs only.
enum PushConfig {
    /// Apple Developer Team ID placeholder (not a secret by itself).
    static let apnsTeamId = "REPLACE_ME_APNS_TEAM_ID"

    /// APNs key id placeholder.
    static let apnsKeyId = "REPLACE_ME_APNS_KEY_ID"

    /// Bundle topic / aps-environment topic placeholder.
    static let apnsTopic = "com.assessment.app"

    /// Backend path that would register a device token once APNs is wired.
    static let deviceRegisterPath = "/api/v1/push/devices"

    /// UserDefaults key for the last stub alert id (debug only).
    static let lastAlertIdKey = "assessment.push.lastAlertId"
}

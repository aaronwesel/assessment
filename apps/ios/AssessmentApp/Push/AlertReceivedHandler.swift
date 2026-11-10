import Foundation
import OSLog

/// Single "alert received" path for the mobile push stub.
///
/// When APNs / UNUserNotificationCenter is wired, route notification userInfo
/// here. Until then, call `onAlertReceived` from debug UI or previews.
///
/// Never log PHI — only opaque alert ids and non-clinical categories.
enum AlertReceivedHandler {
    private static let logger = Logger(
        subsystem: "com.assessment.app",
        category: "push"
    )

    /// Handle an inbound alert stub.
    /// - Parameters:
    ///   - alertId: opaque server id (UUID) — not a patient identifier
    ///   - category: non-clinical category token e.g. "ops", "billing"
    static func onAlertReceived(alertId: String, category: String = "ops") {
        UserDefaults.standard.set(alertId, forKey: PushConfig.lastAlertIdKey)
        logger.info(
            "alert_received id_len=\(alertId.count, privacy: .public) category=\(category, privacy: .public)"
        )
        // TODO: present local notification when push entitlement is enabled.
        // TODO: optionally POST ack to backend /api/v1/push/ack/{alertId}
    }
}

package com.assessment.app.push

/**
 * FCM / push configuration placeholders.
 *
 * Do not commit real server keys or service-account JSON. Inject via
 * local.properties / CI secrets / Secrets Manager when wiring production.
 *
 * HIPAA: push payloads must never include PHI — use opaque alert IDs only.
 */
object PushConfig {
    /** Firebase project id placeholder (not a secret). */
    const val FCM_PROJECT_ID: String = "REPLACE_ME_FCM_PROJECT_ID"

    /**
     * Sender / google-services.json app id placeholder.
     * Real values come from Firebase console + google-services plugin.
     */
    const val FCM_SENDER_ID: String = "REPLACE_ME_FCM_SENDER_ID"

    /** Channel used for the scaffold "alert received" notification. */
    const val ALERT_CHANNEL_ID: String = "assessment_alerts"

    const val ALERT_CHANNEL_NAME: String = "Assessment alerts"

    /**
     * Backend path that would register a device token once FCM is wired.
     * Stub only — not called until the messaging dependency is added.
     */
    const val DEVICE_REGISTER_PATH: String = "/api/v1/push/devices"
}

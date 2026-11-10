package com.assessment.app.push

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.os.Build
import android.util.Log

/**
 * Single "alert received" path for the mobile push stub.
 *
 * When FCM messaging is added, route RemoteMessage data here. Until then,
 * [onAlertReceived] can be invoked from debug UI or instrumentation.
 *
 * Never log PHI — only opaque alert ids and non-clinical categories.
 */
object AlertReceivedHandler {
    private const val TAG = "AssessmentPush"

    fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = context.getSystemService(NotificationManager::class.java) ?: return
        val channel = NotificationChannel(
            PushConfig.ALERT_CHANNEL_ID,
            PushConfig.ALERT_CHANNEL_NAME,
            NotificationManager.IMPORTANCE_DEFAULT,
        ).apply {
            description = "Operational alerts (no PHI)"
        }
        manager.createNotificationChannel(channel)
    }

    /**
     * Handle an inbound alert stub.
     *
     * @param alertId opaque server id (UUID) — not a patient identifier
     * @param category non-clinical category token e.g. "ops", "billing"
     */
    fun onAlertReceived(
        context: Context,
        alertId: String,
        category: String = "ops",
    ) {
        ensureChannel(context)
        // Scaffold: log structured flags only (no email, name, or clinical text).
        Log.i(TAG, "alert_received id_len=${alertId.length} category=$category")
        // TODO: post NotificationCompat when FCM dependency is enabled.
        // TODO: optionally POST ack to backend /api/v1/push/ack/{alertId}
    }
}

package com.assessment.app

import android.app.Application
import com.assessment.app.config.ApiConfig
import com.assessment.app.push.AlertReceivedHandler

class AssessmentApp : Application() {
    override fun onCreate() {
        super.onCreate()
        // Scaffold: confirm API target early in logs (no secrets / PHI).
        android.util.Log.i(TAG, "API base URL = ${ApiConfig.BASE_URL}")
        // Push stub: create the alerts channel early (FCM wiring still TODO).
        AlertReceivedHandler.ensureChannel(this)
    }

    companion object {
        private const val TAG = "AssessmentApp"
    }
}

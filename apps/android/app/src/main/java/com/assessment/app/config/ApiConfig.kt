package com.assessment.app.config

import com.assessment.app.BuildConfig

/**
 * Shared FastAPI backend base URL.
 *
 * Emulator → host: http://10.0.2.2:8000
 * Device → LAN:    http://<lan-ip>:8000
 * Staging:         https://api.example.com
 *
 * Override with -PAPI_BASE_URL=... at Gradle build time (see README).
 */
object ApiConfig {
    val BASE_URL: String = BuildConfig.API_BASE_URL.trimEnd('/')

    // Placeholder paths — align with backend OpenAPI when auth stubs land.
    const val LOGIN_PASSWORD_PATH = "/auth/login"
    const val LOGIN_GOOGLE_PATH = "/auth/google"
    const val LOGIN_MICROSOFT_PATH = "/auth/microsoft"
}

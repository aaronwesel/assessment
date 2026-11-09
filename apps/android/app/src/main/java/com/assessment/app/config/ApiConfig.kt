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

    // Auth API paths (same contract as web / iOS).
    const val LOGIN_PASSWORD_PATH = "/api/v1/auth/login"
    const val REGISTER_PASSWORD_PATH = "/api/v1/auth/register"
    const val LOGIN_GOOGLE_PATH = "/api/v1/auth/google/onetap"
    const val LOGIN_MICROSOFT_PATH = "/api/v1/auth/microsoft/onetap"
    const val AUTH_ME_PATH = "/api/v1/auth/me"
    const val AUTH_REFRESH_PATH = "/api/v1/auth/refresh"
    const val AUTH_LOGOUT_PATH = "/api/v1/auth/logout"
}

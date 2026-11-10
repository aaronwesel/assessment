package com.assessment.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.assessment.app.ui.login.LoginScreen
import com.assessment.app.ui.theme.AssessmentTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            AssessmentTheme {
                LoginScreen()
            }
        }
    }
}

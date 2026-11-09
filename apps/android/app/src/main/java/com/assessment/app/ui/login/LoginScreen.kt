package com.assessment.app.ui.login

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.assessment.app.config.ApiConfig
import com.assessment.app.ui.theme.AssessmentTheme

/**
 * Scaffold login UI only.
 * Google / Microsoft One Tap and password submit are not wired to SDKs or the API yet.
 */
@Composable
fun LoginScreen(
    onGoogleOneTap: () -> Unit = { /* TODO: Credential Manager / GIS → ${ApiConfig.LOGIN_GOOGLE_PATH} */ },
    onMicrosoftOneTap: () -> Unit = { /* TODO: MSAL → ${ApiConfig.LOGIN_MICROSOFT_PATH} */ },
    onPasswordLogin: (email: String, password: String) -> Unit = { _, _ ->
        /* TODO: POST ${ApiConfig.BASE_URL}${ApiConfig.LOGIN_PASSWORD_PATH} */
    },
) {
    var email by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(
            text = "Assessment",
            style = MaterialTheme.typography.headlineMedium,
        )
        Text(
            text = "Sign in (scaffold)",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = "API: ${ApiConfig.BASE_URL}",
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Spacer(Modifier.height(32.dp))

        OutlinedButton(
            onClick = onGoogleOneTap,
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text("Continue with Google (One Tap stub)")
        }
        Spacer(Modifier.height(12.dp))
        OutlinedButton(
            onClick = onMicrosoftOneTap,
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text("Continue with Microsoft (One Tap stub)")
        }

        Spacer(Modifier.height(24.dp))
        HorizontalDivider()
        Spacer(Modifier.height(24.dp))

        OutlinedTextField(
            value = email,
            onValueChange = { email = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Email") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email),
        )
        Spacer(Modifier.height(12.dp))
        OutlinedTextField(
            value = password,
            onValueChange = { password = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Password") },
            singleLine = true,
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
        )
        Spacer(Modifier.height(16.dp))
        Button(
            onClick = { onPasswordLogin(email, password) },
            modifier = Modifier.fillMaxWidth(),
            enabled = email.isNotBlank() && password.isNotBlank(),
        ) {
            Text("Sign in with password (stub)")
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun LoginScreenPreview() {
    AssessmentTheme {
        LoginScreen()
    }
}

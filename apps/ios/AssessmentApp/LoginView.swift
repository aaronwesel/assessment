import SwiftUI

/// Scaffold login UI only.
/// Google / Microsoft One Tap and password submit are not wired to SDKs or the API yet.
struct LoginView: View {
    @State private var email = ""
    @State private var password = ""

    var onGoogleOneTap: () -> Void = {
        // TODO: Google Sign-In → ApiConfig.loginGooglePath
    }
    var onMicrosoftOneTap: () -> Void = {
        // TODO: MSAL → ApiConfig.loginMicrosoftPath
    }
    var onPasswordLogin: (_ email: String, _ password: String) -> Void = { _, _ in
        // TODO: POST ApiConfig.baseURL + ApiConfig.loginPasswordPath
    }

    var body: some View {
        NavigationStack {
            VStack(spacing: 16) {
                Text("Assessment")
                    .font(.largeTitle.weight(.semibold))
                Text("Sign in (scaffold)")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                Text("API: \(ApiConfig.baseURL)")
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
                    .padding(.bottom, 8)

                Button("Continue with Google (One Tap stub)", action: onGoogleOneTap)
                    .buttonStyle(.bordered)
                    .frame(maxWidth: .infinity)

                Button("Continue with Microsoft (One Tap stub)", action: onMicrosoftOneTap)
                    .buttonStyle(.bordered)
                    .frame(maxWidth: .infinity)

                Divider()
                    .padding(.vertical, 8)

                TextField("Email", text: $email)
                    .textContentType(.emailAddress)
                    .keyboardType(.emailAddress)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .textFieldStyle(.roundedBorder)

                SecureField("Password", text: $password)
                    .textContentType(.password)
                    .textFieldStyle(.roundedBorder)

                Button("Sign in with password (stub)") {
                    onPasswordLogin(email, password)
                }
                .buttonStyle(.borderedProminent)
                .frame(maxWidth: .infinity)
                .disabled(email.isEmpty || password.isEmpty)

                Spacer()
            }
            .padding(24)
            .navigationBarTitleDisplayMode(.inline)
        }
    }
}

#Preview {
    LoginView()
}

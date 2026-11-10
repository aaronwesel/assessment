import SwiftUI

@main
struct AssessmentAppApp: App {
    init() {
        // Scaffold: confirm API target early (no secrets / PHI).
        print("API base URL = \(ApiConfig.baseURL)")
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}

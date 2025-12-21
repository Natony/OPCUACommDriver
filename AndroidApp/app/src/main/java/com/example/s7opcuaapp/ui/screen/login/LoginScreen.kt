package com.example.s7opcuaapp.ui.screen.login

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.example.s7opcuaapp.R
import com.example.s7opcuaapp.ui.components.CommonTextField

@Composable
fun LoginScreen(
    uiState: LoginUiState,
    onUsernameChanged: (String) -> Unit,
    onPasswordChanged: (String) -> Unit,
    onTogglePasswordVisibility: () -> Unit,
    onLoginClicked: () -> Unit
) {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Card(
            modifier = Modifier
                .widthIn(max = 400.dp)
                .padding(16.dp),
            elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Logo or App Icon
                Image(
                    painter = painterResource(id = R.drawable.ic_logo1),
                    contentDescription = "App Logo",
                    modifier = Modifier.size(80.dp)
                )

                // Title
                Text(
                    text = "AVS Shuttle",
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold
                )

                Spacer(modifier = Modifier.height(8.dp))

                // Username field
                CommonTextField(
                    label = "Username",
                    value = uiState.username,
                    onValueChange = onUsernameChanged
                )

                // Password field
                CommonTextField(
                    label = "Password",
                    value = uiState.password,
                    onValueChange = onPasswordChanged,
                    isPassword = true,
                    isPasswordVisible = uiState.isPasswordVisible,
                    onTogglePasswordVisibility = onTogglePasswordVisibility
                )

                // Error message
                uiState.errorMessage?.let { msg ->
                    Card(
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.errorContainer
                        ),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            text = msg,
                            color = MaterialTheme.colorScheme.onErrorContainer,
                            modifier = Modifier.padding(12.dp),
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                // Login button
                Button(
                    onClick = onLoginClicked,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    enabled = !uiState.isLoading
                ) {
                    if (uiState.isLoading) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(24.dp),
                            color = MaterialTheme.colorScheme.onPrimary,
                            strokeWidth = 2.dp
                        )
                    } else {
                        Text("LOGIN")
                    }
                }
            }
        }
    }
}

// ========== PREVIEW SECTION ==========
@Preview(showBackground = true, widthDp = 1920, heightDp = 1200)
@Composable
fun LoginScreenLoadingPreview() {
    val sampleState = LoginUiState(
        username = "admin",
        password = "password",
        isPasswordVisible = false,
        isLoading = true,
        errorMessage = null
    )

    LoginScreen(
        uiState = sampleState,
        onUsernameChanged = {},
        onPasswordChanged = {},
        onTogglePasswordVisibility = {},
        onLoginClicked = {}
    )
}

@Preview(showBackground = true, widthDp = 1920, heightDp = 1200)
@Composable
fun LoginScreenErrorPreview() {
    val sampleState = LoginUiState(
        username = "admin",
        password = "",
        isPasswordVisible = false,
        isLoading = false,
        errorMessage = "Invalid credentials. Please try again."
    )

    LoginScreen(
        uiState = sampleState,
        onUsernameChanged = {},
        onPasswordChanged = {},
        onTogglePasswordVisibility = {},
        onLoginClicked = {}
    )
}
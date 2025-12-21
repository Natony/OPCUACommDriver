package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.input.KeyboardType
import com.example.s7opcuaapp.R

/**
 * Wrapper cho OutlinedTextField:
 * - label: nhãn
 * - value: giá trị
 * - onValueChange: callback
 * - isPassword: nếu true thì hiển thị/ẩn mật khẩu
 * - isPasswordVisible & onTogglePasswordVisibility: để toggle
 */
@Composable
fun CommonTextField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    isPassword: Boolean = false,
    isPasswordVisible: Boolean = false,
    onTogglePasswordVisibility: () -> Unit = {}
) {
    OutlinedTextField(
        value = value,
        onValueChange = { newValue ->
            // Nếu không phải password field, tự động trim
            if (!isPassword) {
                onValueChange(newValue.trim())
            } else {
                onValueChange(newValue) // Password giữ nguyên, không trim
            }
        },
        label = { Text(label) },
        singleLine = true,
        keyboardOptions = KeyboardOptions(
            keyboardType = if (isPassword) KeyboardType.Password else KeyboardType.Text
        ),
        visualTransformation = if (isPassword && !isPasswordVisible) {
            PasswordVisualTransformation()
        } else {
            VisualTransformation.None
        },
        trailingIcon = {
            if (isPassword) {
                IconButton(onClick = { onTogglePasswordVisibility() }) {
                    val iconRes = if (isPasswordVisible)
                        R.drawable.ic_visibility_on
                    else
                        R.drawable.ic_visibility_off
                    Icon(painter = painterResource(id = iconRes), contentDescription = null)
                }
            }
        },
        modifier = Modifier.fillMaxWidth()
    )
}
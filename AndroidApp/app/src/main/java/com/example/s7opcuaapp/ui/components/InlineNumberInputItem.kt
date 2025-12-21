package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.Divider
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.Alignment
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign

/**
 * InlineNumberInputItem hiển thị label nằm trên cùng,
 * rồi mới đến ô nhập số phía dưới với gạch dưới.
 */
@Composable
fun InlineNumberInputItem(
//    label: String,
    textValue: String,
    isWriting: Boolean,
    onTextChange: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
//        Text(
//            text = label,
//            fontSize = 14.sp,
//            modifier = Modifier.padding(bottom = 8.dp)
//        )

        Box(
            modifier = Modifier.fillMaxWidth(),
            contentAlignment = Alignment.Center
        ) {
            Column(
                modifier = Modifier.fillMaxWidth()
            ) {
                BasicTextField(
                    value = textValue,
                    onValueChange = { new ->
                        // Cho phép cả số nguyên và số thập phân
                        if (new.isEmpty() || new.matches(Regex("^\\d*\\.?\\d*$"))) {
                            onTextChange(new)
                        }
                    },
                    textStyle = TextStyle(
                        fontSize = 16.sp,
                        color = if (isWriting) Color.Gray else MaterialTheme.colorScheme.onSurface,
                        textAlign = TextAlign.Center
                    ),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 4.dp),
                    enabled = !isWriting,
                    decorationBox = { innerTextField ->
                        Box(
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            if (textValue.isEmpty()) {
                                Text(
                                    text = "...",
                                    style = TextStyle(
                                        fontSize = 16.sp,
                                        color = Color.Gray
                                    )
                                )
                            }
                            innerTextField()
                        }
                    }
                )

                // Gạch dưới
                Divider(
                    modifier = Modifier.fillMaxWidth(),
                    thickness = 1.dp,
                    color = if (isWriting) MaterialTheme.colorScheme.primary else Color.Gray
                )
            }

            if (isWriting) {
                CircularProgressIndicator(
                    modifier = Modifier
                        .size(20.dp)
                        .align(Alignment.Center),
                    strokeWidth = 2.dp
                )
            }
        }
    }
}
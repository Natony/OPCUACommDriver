package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Dropdown selector cho danh sách chức năng.
 * Tiết kiệm không gian hơn so với liệt kê cứng.
 *
 * @param entries        List các cặp (label, code)
 * @param selectedCode   Mã chức năng đang được chọn
 * @param onSelect       Callback khi chọn một mục, trả về code tương ứng
 * @param modifier       Modifier tuỳ chỉnh
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FunctionListSelector(
    entries: List<Pair<String, Int>>,
    selectedCode: Int?,
    onSelect: (Int) -> Unit,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }
    val selectedLabel = entries.find { it.second == selectedCode }?.first ?: "Chọn chức năng"

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = !expanded },
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 1.dp)
    ) {
        TextField(
            value = selectedLabel,
            onValueChange = {},
            readOnly = true,
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            colors = ExposedDropdownMenuDefaults.textFieldColors(),
            modifier = Modifier
                .menuAnchor()  // ensure anchor for dropdown
                .fillMaxWidth()
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            modifier = Modifier.fillMaxWidth()
        ) {
            entries.forEach { (label, code) ->
                DropdownMenuItem(
                    text = { Text(text = label, fontSize = 8.sp) },
                    onClick = {
                        onSelect(code)
                        expanded = false
                    }
                )
            }
        }
    }
}

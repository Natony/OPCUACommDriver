package com.example.s7opcuaapp.ui.navigation

import androidx.compose.foundation.layout.height
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.example.s7opcuaapp.R

/**
 * Các item cho BottomNavBar.
 *
 * Sử dụng selected = true khi route hiện tại khớp.
 */
sealed class BottomNavItem(val route: String, val iconRes: Int, val title: String) {
    object Control : BottomNavItem("control", R.drawable.ic_control, "Control")
    object Home    : BottomNavItem("home",    R.drawable.ic_home,    "Home")
    object Alarm   : BottomNavItem("alarm",   R.drawable.ic_alarm,   "Alarm")
    object Config  : BottomNavItem("config_btm", R.drawable.ic_settings, "Config")
}

@Composable
fun BottomNavBar(navController: NavController) {
    val items = listOf(
        BottomNavItem.Control,
        BottomNavItem.Home,
        BottomNavItem.Alarm,
        BottomNavItem.Config
    )

    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route

    NavigationBar(modifier = Modifier.height(56.dp)) {
        items.forEach { item ->
            NavigationBarItem(
                icon = { Icon(painter = painterResource(id = item.iconRes), contentDescription = item.title) },
                label = { Text(text = item.title) },
                selected = currentRoute == item.route,
                onClick = {
                    if (currentRoute != item.route) {
                        navController.navigate(item.route) {
                            launchSingleTop = true
                            restoreState = true
                            // popUpTo("control") {} có thể thêm nếu cần
                        }
                    }
                }
            )
        }
    }
}

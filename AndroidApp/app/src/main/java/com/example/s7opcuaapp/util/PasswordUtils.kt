package com.example.s7opcuaapp.util

import java.security.MessageDigest
import kotlin.experimental.and

object PasswordUtils {

    enum class PasswordStrength {
        WEAK,
        MEDIUM,
        STRONG
    }

    fun hashPassword(password: String): String {
        val bytes = MessageDigest.getInstance("SHA-256").digest(password.toByteArray())
        val hash = bytes.joinToString("") { "%02x".format(it) }
        println("🔐 Password hashing:")
        println("   Input: $password")
        println("   Hash: $hash")
        return hash
    }

    fun verifyPassword(password: String, passwordHash: String): Boolean {
        val inputHash = hashPassword(password)
        val isMatch = inputHash == passwordHash
        println("🔍 Password verification:")
        println("   Input: $password")
        println("   Input hash: $inputHash")
        println("   Stored hash: $passwordHash")
        println("   Match: $isMatch")
        return isMatch
    }

    fun isValidPassword(password: String): Boolean {
        if (password.length < 6) {
            println("❌ Password too short: ${password.length} < 6")
            return false
        }

        var hasLetter = false
        var hasDigit = false

        password.forEach { char ->
            when {
                char.isLetter() -> hasLetter = true
                char.isDigit() -> hasDigit = true
            }
        }

        val isValid = hasLetter && hasDigit
        println("🔍 Password validation for '$password':")
        println("   Length: ${password.length} (${if (password.length >= 6) "✅" else "❌"})")
        println("   Has letter: $hasLetter (${if (hasLetter) "✅" else "❌"})")
        println("   Has digit: $hasDigit (${if (hasDigit) "✅" else "❌"})")
        println("   Valid: $isValid")

        return isValid
    }

    fun getPasswordStrength(password: String): PasswordStrength {
        var strength = 0

        if (password.length >= 8) strength++
        if (password.length >= 12) strength++
        if (password.any { it.isUpperCase() }) strength++
        if (password.any { it.isLowerCase() }) strength++
        if (password.any { it.isDigit() }) strength++
        if (password.any { !it.isLetterOrDigit() }) strength++

        return when {
            strength <= 2 -> PasswordStrength.WEAK
            strength <= 4 -> PasswordStrength.MEDIUM
            else -> PasswordStrength.STRONG
        }
    }
}
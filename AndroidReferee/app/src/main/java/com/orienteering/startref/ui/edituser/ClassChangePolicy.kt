package com.orienteering.startref.ui.edituser

import com.orienteering.startref.data.local.ClassEntry

/**
 * Which classes may change via the Edit dialog, and to which targets.
 *
 * - **DIR / OPEN** (prefix, case-insensitive): may switch only among classes in that same bucket.
 * - **M8 / W8 / M08 / W08** youth groups: may switch among classes in that bucket only; excludes age bands
 *   where the next character after the prefix is a digit (e.g. M80, W80, M85, W85).
 */
object ClassChangePolicy {

    fun isClassChangeAllowed(runnerClassName: String): Boolean = classGroupOf(runnerClassName) != null

    fun allowedClassTargets(runnerClassName: String, all: List<ClassEntry>): List<ClassEntry> {
        val g = classGroupOf(runnerClassName) ?: return emptyList()
        return all.filter { classGroupOf(it.className) == g }
    }

    fun classGroupOf(className: String): String? {
        val trimmed = className.trim()
        if (trimmed.isEmpty()) return null
        when {
            trimmed.startsWith("DIR", ignoreCase = true) -> return "diropen"
            trimmed.startsWith("OPEN", ignoreCase = true) -> return "diropen"
            isYouth8Class(trimmed) -> return "youth8"
        }
        return null
    }

    /**
     * M8 / W8 / M08 / W08 youth classes where the segment after the prefix does not start with a digit
     * (excludes M80, W80, M85, W85, etc.).
     */
    fun isYouth8Class(className: String): Boolean {
        val upper = className.uppercase()
        for (prefix in listOf("M08", "W08", "M8", "W8")) {
            if (upper.startsWith(prefix)) {
                val afterPrefix = upper.drop(prefix.length)
                return afterPrefix.isEmpty() || !afterPrefix[0].isDigit()
            }
        }
        return false
    }
}

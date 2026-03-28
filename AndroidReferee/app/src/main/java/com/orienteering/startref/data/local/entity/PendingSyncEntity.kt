package com.orienteering.startref.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

/**
 * Queues outbound PATCH calls for offline-resilient sync.
 * Each row represents one PATCH /api/competitions/{date}/runners/{startNumber} call.
 * The payload is a JSON object matching PatchRunnerRequest.
 */
@Entity(tableName = "pending_sync")
data class PendingSyncEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val type: String,
    val competitionDate: String,   // yyyy-MM-dd
    val startNumber: Int,
    val payload: String,           // JSON: PatchRunnerRequest fields
    val createdAt: Long = System.currentTimeMillis(),
    val retryCount: Int = 0,
    val status: String = STATUS_PENDING
) {
    companion object {
        const val STATUS_PENDING = "PENDING"
        const val STATUS_SENT = "SENT"
        const val STATUS_FAILED = "FAILED"
        const val TYPE_STATUS = "STATUS"    // statusId change (Started/DNS)
        const val TYPE_EDIT = "EDIT"        // other field edits
    }
}

package com.orienteering.startref.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "runners")
data class RunnerEntity(
    @PrimaryKey val startNumber: Int,
    val name: String,
    val surname: String,
    val siCard: String,
    val className: String,
    val clubName: String,
    val startTime: Long,
    val checkedIn: Boolean = false,
    val checkedInAt: Long? = null,
    val dns: Boolean = false
)

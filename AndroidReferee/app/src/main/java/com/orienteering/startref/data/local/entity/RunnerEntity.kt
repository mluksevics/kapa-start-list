package com.orienteering.startref.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "runners")
data class RunnerEntity(
    @PrimaryKey val startNumber: Int,
    val name: String,
    val surname: String,
    val siCard: String,
    val classId: Int = 0,
    val className: String,
    val clubId: Int = 0,
    val clubName: String,
    val startTime: Long,
    val statusId: Int = 1,            // 1=Registered, 2=Started, 3=DNS
    val checkedInAt: Long? = null,    // kept for historical reference; set when status→Started
    val lastModifiedAt: Long = System.currentTimeMillis(),
    val lastModifiedBy: String = "local"
)

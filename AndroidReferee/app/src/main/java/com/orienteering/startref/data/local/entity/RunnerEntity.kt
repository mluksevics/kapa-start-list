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
    val country: String = "",
    val startPlace: Int = 0,
    val statusId: Int = 1,            // 1=Registered, 2=Started, 3=DNS
    val checkedInAt: Long? = null,    // kept for historical reference; set when status→Started
    val lastModifiedAt: Long = System.currentTimeMillis(),
    val lastModifiedBy: String = "local"
)

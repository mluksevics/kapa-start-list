package com.orienteering.startref.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "class_lookups")
data class ClassLookupEntity(
    @PrimaryKey val id: Int,
    val name: String,
    val startPlace: Int = 0
)

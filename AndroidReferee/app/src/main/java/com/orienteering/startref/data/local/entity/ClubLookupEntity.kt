package com.orienteering.startref.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "club_lookups")
data class ClubLookupEntity(
    @PrimaryKey val id: Int,
    val name: String
)

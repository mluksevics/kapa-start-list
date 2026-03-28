package com.orienteering.startref.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.orienteering.startref.data.local.entity.ClassLookupEntity
import com.orienteering.startref.data.local.entity.ClubLookupEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface LookupDao {
    @Query("SELECT * FROM class_lookups ORDER BY name ASC")
    fun observeAllClasses(): Flow<List<ClassLookupEntity>>

    @Query("SELECT * FROM club_lookups ORDER BY name ASC")
    fun observeAllClubs(): Flow<List<ClubLookupEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertClasses(items: List<ClassLookupEntity>)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertClubs(items: List<ClubLookupEntity>)

    @Query("DELETE FROM class_lookups")
    suspend fun deleteAllClasses()

    @Query("DELETE FROM club_lookups")
    suspend fun deleteAllClubs()
}

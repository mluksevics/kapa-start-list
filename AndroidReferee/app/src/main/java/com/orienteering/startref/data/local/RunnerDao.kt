package com.orienteering.startref.data.local

import androidx.room.ColumnInfo
import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import com.orienteering.startref.data.local.entity.RunnerEntity
import kotlinx.coroutines.flow.Flow

data class ClassEntry(
    @ColumnInfo(name = "classId") val classId: Int,
    @ColumnInfo(name = "className") val className: String
)

@Dao
interface RunnerDao {

    @Query("SELECT * FROM runners ORDER BY startTime ASC, startNumber ASC")
    fun observeAll(): Flow<List<RunnerEntity>>

    @Query("SELECT * FROM runners ORDER BY startTime ASC, startNumber ASC")
    suspend fun getAll(): List<RunnerEntity>

    @Query("SELECT * FROM runners WHERE startNumber = :startNumber LIMIT 1")
    suspend fun getByStartNumber(startNumber: Int): RunnerEntity?

    @Query("SELECT DISTINCT classId, className FROM runners ORDER BY className ASC")
    fun observeDistinctClasses(): Flow<List<ClassEntry>>

    @Query("SELECT * FROM runners WHERE siCard = :siCard LIMIT 1")
    suspend fun getBySiCard(siCard: String): RunnerEntity?

    @Query("UPDATE runners SET className = :className WHERE classId = :classId AND className != :className")
    suspend fun updateClassNameByClassId(classId: Int, className: String): Int

    @Query("UPDATE runners SET clubName = :clubName WHERE clubId = :clubId AND clubName != :clubName")
    suspend fun updateClubNameByClubId(clubId: Int, clubName: String): Int

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insert(runner: RunnerEntity)

    @Update
    suspend fun update(runner: RunnerEntity)

    @Query("DELETE FROM runners")
    suspend fun deleteAll()
}

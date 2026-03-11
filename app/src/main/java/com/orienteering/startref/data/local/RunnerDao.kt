package com.orienteering.startref.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import com.orienteering.startref.data.local.entity.RunnerEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface RunnerDao {

    @Query("SELECT * FROM runners ORDER BY startTime ASC, startNumber ASC")
    fun observeAll(): Flow<List<RunnerEntity>>

    @Query("SELECT * FROM runners ORDER BY startTime ASC, startNumber ASC")
    suspend fun getAll(): List<RunnerEntity>

    @Query("SELECT * FROM runners WHERE startNumber = :startNumber LIMIT 1")
    suspend fun getByStartNumber(startNumber: Int): RunnerEntity?

    @Query("SELECT DISTINCT className FROM runners ORDER BY className ASC")
    fun observeDistinctClasses(): Flow<List<String>>

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insert(runner: RunnerEntity)

    @Update
    suspend fun update(runner: RunnerEntity)

    @Query("""
        UPDATE runners SET
            name = :name, surname = :surname, siCard = :siCard,
            className = :className, clubName = :clubName, startTime = :startTime
        WHERE startNumber = :startNumber
    """)
    suspend fun updateXmlFields(
        startNumber: Int,
        name: String,
        surname: String,
        siCard: String,
        className: String,
        clubName: String,
        startTime: Long
    )

    @Query("DELETE FROM runners")
    suspend fun deleteAll()
}

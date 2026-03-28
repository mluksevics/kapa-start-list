package com.orienteering.startref.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.Query
import androidx.room.Update
import com.orienteering.startref.data.local.entity.PendingSyncEntity
import kotlinx.coroutines.flow.Flow

@Dao
interface PendingSyncDao {

    @Query("SELECT * FROM pending_sync WHERE status != 'SENT' ORDER BY createdAt ASC")
    suspend fun getPending(): List<PendingSyncEntity>

    @Query("SELECT * FROM pending_sync WHERE id = :id LIMIT 1")
    suspend fun getById(id: Long): PendingSyncEntity?

    @Insert
    suspend fun insert(entity: PendingSyncEntity): Long

    @Update
    suspend fun update(entity: PendingSyncEntity)

    @Query("UPDATE pending_sync SET status = 'SENT' WHERE id = :id")
    suspend fun markSent(id: Long)

    @Query("UPDATE pending_sync SET status = 'FAILED', retryCount = retryCount + 1 WHERE id = :id")
    suspend fun markFailed(id: Long)

    @Query("DELETE FROM pending_sync WHERE status = 'SENT'")
    suspend fun deleteSent()

    @Query("DELETE FROM pending_sync")
    suspend fun deleteAll()

    @Query("SELECT COUNT(*) FROM pending_sync WHERE status = 'SENT'")
    fun observeSentCount(): Flow<Int>

    @Query("SELECT COUNT(*) FROM pending_sync")
    fun observeTotalCount(): Flow<Int>
}

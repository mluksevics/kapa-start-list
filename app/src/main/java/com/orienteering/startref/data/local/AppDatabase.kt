package com.orienteering.startref.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import com.orienteering.startref.data.local.entity.PendingSyncEntity
import com.orienteering.startref.data.local.entity.RunnerEntity

@Database(
    entities = [RunnerEntity::class, PendingSyncEntity::class],
    version = 2,
    exportSchema = false
)
abstract class AppDatabase : RoomDatabase() {
    abstract fun runnerDao(): RunnerDao
    abstract fun pendingSyncDao(): PendingSyncDao

    companion object {
        val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE runners ADD COLUMN dns INTEGER NOT NULL DEFAULT 0")
            }
        }
    }
}

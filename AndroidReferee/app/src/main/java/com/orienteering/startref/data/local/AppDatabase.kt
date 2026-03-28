package com.orienteering.startref.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import com.orienteering.startref.data.local.entity.PendingSyncEntity
import com.orienteering.startref.data.local.entity.RunnerEntity

@Database(
    entities = [RunnerEntity::class, PendingSyncEntity::class],
    version = 4,
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

        val MIGRATION_3_4 = object : Migration(3, 4) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE runners ADD COLUMN classId INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE runners ADD COLUMN clubId INTEGER NOT NULL DEFAULT 0")
            }
        }

        val MIGRATION_2_3 = object : Migration(2, 3) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE runners ADD COLUMN statusId INTEGER NOT NULL DEFAULT 1")
                db.execSQL("ALTER TABLE runners ADD COLUMN country TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE runners ADD COLUMN startPlace INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE runners ADD COLUMN lastModifiedAt INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE runners ADD COLUMN lastModifiedBy TEXT NOT NULL DEFAULT 'local'")
                // Migrate old boolean flags to statusId
                db.execSQL("UPDATE runners SET statusId = 2 WHERE checkedIn = 1")
                db.execSQL("UPDATE runners SET statusId = 3 WHERE dns = 1")
                db.execSQL("UPDATE runners SET lastModifiedAt = COALESCE(checkedInAt, 0)")
                // checkedIn and dns columns remain but are no longer read by the entity
            }
        }
    }
}

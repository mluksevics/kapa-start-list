package com.orienteering.startref.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import com.orienteering.startref.data.local.entity.ClassLookupEntity
import com.orienteering.startref.data.local.entity.ClubLookupEntity
import com.orienteering.startref.data.local.entity.PendingSyncEntity
import com.orienteering.startref.data.local.entity.RunnerEntity

@Database(
    entities = [RunnerEntity::class, PendingSyncEntity::class, ClassLookupEntity::class, ClubLookupEntity::class],
    version = 7,
    exportSchema = false
)
abstract class AppDatabase : RoomDatabase() {
    abstract fun runnerDao(): RunnerDao
    abstract fun pendingSyncDao(): PendingSyncDao
    abstract fun lookupDao(): LookupDao

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

        // Recreates runners table to remove legacy columns (checkedIn, dns) and SQL DEFAULT
        // constraints that were added via ALTER TABLE but are not part of the entity schema.
        // Also recreates pending_sync to add missing startNumber and competitionDate columns
        // (NOT NULL, no default — old rows are discarded as they cannot be populated).
        val MIGRATION_4_5 = object : Migration(4, 5) {
            override fun migrate(db: SupportSQLiteDatabase) {
                // --- runners ---
                db.execSQL("""
                    CREATE TABLE IF NOT EXISTS `runners_new` (
                        `startNumber` INTEGER NOT NULL,
                        `name` TEXT NOT NULL,
                        `surname` TEXT NOT NULL,
                        `siCard` TEXT NOT NULL,
                        `classId` INTEGER NOT NULL,
                        `className` TEXT NOT NULL,
                        `clubId` INTEGER NOT NULL,
                        `clubName` TEXT NOT NULL,
                        `startTime` INTEGER NOT NULL,
                        `country` TEXT NOT NULL,
                        `startPlace` INTEGER NOT NULL,
                        `statusId` INTEGER NOT NULL,
                        `checkedInAt` INTEGER,
                        `lastModifiedAt` INTEGER NOT NULL,
                        `lastModifiedBy` TEXT NOT NULL,
                        PRIMARY KEY(`startNumber`)
                    )
                """.trimIndent())
                db.execSQL("""
                    INSERT INTO `runners_new`
                        (startNumber, name, surname, siCard, classId, className, clubId, clubName,
                         startTime, country, startPlace, statusId, checkedInAt, lastModifiedAt, lastModifiedBy)
                    SELECT startNumber, name, surname, siCard, classId, className, clubId, clubName,
                           startTime, country, startPlace, statusId, checkedInAt, lastModifiedAt, lastModifiedBy
                    FROM runners
                """.trimIndent())
                db.execSQL("DROP TABLE runners")
                db.execSQL("ALTER TABLE runners_new RENAME TO runners")

                // --- pending_sync ---
                // Old table is missing startNumber and competitionDate (NOT NULL, no default).
                // Rows cannot be migrated — discard and recreate with correct schema.
                db.execSQL("DROP TABLE IF EXISTS `pending_sync`")
                db.execSQL("""
                    CREATE TABLE IF NOT EXISTS `pending_sync` (
                        `id` INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                        `type` TEXT NOT NULL,
                        `competitionDate` TEXT NOT NULL,
                        `startNumber` INTEGER NOT NULL,
                        `payload` TEXT NOT NULL,
                        `createdAt` INTEGER NOT NULL,
                        `retryCount` INTEGER NOT NULL,
                        `status` TEXT NOT NULL
                    )
                """.trimIndent())
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

        val MIGRATION_5_6 = object : Migration(5, 6) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS `class_lookups` (
                        `id` INTEGER NOT NULL,
                        `name` TEXT NOT NULL,
                        PRIMARY KEY(`id`)
                    )
                    """.trimIndent()
                )
                db.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS `club_lookups` (
                        `id` INTEGER NOT NULL,
                        `name` TEXT NOT NULL,
                        PRIMARY KEY(`id`)
                    )
                    """.trimIndent()
                )
            }
        }

        val MIGRATION_6_7 = object : Migration(6, 7) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS `runners_new` (
                        `startNumber` INTEGER NOT NULL,
                        `name` TEXT NOT NULL,
                        `surname` TEXT NOT NULL,
                        `siCard` TEXT NOT NULL,
                        `classId` INTEGER NOT NULL,
                        `className` TEXT NOT NULL,
                        `clubId` INTEGER NOT NULL,
                        `clubName` TEXT NOT NULL,
                        `startTime` INTEGER NOT NULL,
                        `statusId` INTEGER NOT NULL,
                        `checkedInAt` INTEGER,
                        `lastModifiedAt` INTEGER NOT NULL,
                        `lastModifiedBy` TEXT NOT NULL,
                        PRIMARY KEY(`startNumber`)
                    )
                    """.trimIndent()
                )
                db.execSQL(
                    """
                    INSERT INTO `runners_new`
                        (startNumber, name, surname, siCard, classId, className, clubId, clubName,
                         startTime, statusId, checkedInAt, lastModifiedAt, lastModifiedBy)
                    SELECT startNumber, name, surname, siCard, classId, className, clubId, clubName,
                           startTime, statusId, checkedInAt, lastModifiedAt, lastModifiedBy
                    FROM runners
                    """.trimIndent()
                )
                db.execSQL("DROP TABLE runners")
                db.execSQL("ALTER TABLE runners_new RENAME TO runners")
                db.execSQL(
                    "ALTER TABLE class_lookups ADD COLUMN startPlace INTEGER NOT NULL DEFAULT 0"
                )
            }
        }
    }
}

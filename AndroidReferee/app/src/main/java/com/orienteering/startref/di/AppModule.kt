package com.orienteering.startref.di

import android.content.Context
import androidx.room.Room
import androidx.work.WorkManager
import com.orienteering.startref.data.local.AppDatabase
import com.orienteering.startref.data.local.PendingSyncDao
import com.orienteering.startref.data.local.RunnerDao
import com.orienteering.startref.data.remote.ApiClient
import com.orienteering.startref.data.si.SiStationReader
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object AppModule {

    @Provides
    @Singleton
    fun provideOkHttpClient(): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .build()

    @Provides
    @Singleton
    fun provideApiClient(okHttpClient: OkHttpClient): ApiClient = ApiClient(okHttpClient)

    @Provides
    @Singleton
    fun provideDatabase(@ApplicationContext context: Context): AppDatabase =
        Room.databaseBuilder(context, AppDatabase::class.java, "startref.db")
            .addMigrations(AppDatabase.MIGRATION_1_2, AppDatabase.MIGRATION_2_3, AppDatabase.MIGRATION_3_4)
            .build()

    @Provides
    fun provideRunnerDao(db: AppDatabase): RunnerDao = db.runnerDao()

    @Provides
    fun providePendingSyncDao(db: AppDatabase): PendingSyncDao = db.pendingSyncDao()

    @Provides
    @Singleton
    fun provideWorkManager(@ApplicationContext context: Context): WorkManager =
        WorkManager.getInstance(context)

    @Provides
    @Singleton
    fun provideSiStationReader(@ApplicationContext context: Context): SiStationReader =
        SiStationReader(context)
}

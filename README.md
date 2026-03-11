# AndroidStart1

Android app in Kotlin using Jetpack Compose.

## Stack

- Kotlin
- Jetpack Compose
- Hilt (DI)
- Room (local DB)
- WorkManager (background sync)
- Gradle Kotlin DSL

## Project layout

- `app/` main Android module
- `app/src/main/java/com/orienteering/startref/` app code
- `app/src/main/res/` resources (strings, themes, drawables)
- `app/src/main/AndroidManifest.xml` app manifest + permissions
- `gradle/libs.versions.toml` dependency and plugin versions
- `settings.gradle.kts` module list
- `build.gradle.kts` root build config

## Run

1. Open in Android Studio.
2. Sync Gradle.
3. Run `app` on emulator/device.

CLI:

```powershell
.\gradlew.bat assembleDebug
```

## Notes

- `local.properties` is machine-specific and ignored.
- Build artifacts and IDE caches are ignored via `.gitignore`.

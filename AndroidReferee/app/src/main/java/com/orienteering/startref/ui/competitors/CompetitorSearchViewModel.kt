package com.orienteering.startref.ui.competitors

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.orienteering.startref.data.local.ClassEntry
import com.orienteering.startref.data.local.ClubEntry
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.repository.StartListRepository
import com.orienteering.startref.data.settings.AppSettings
import com.orienteering.startref.data.settings.SettingsDataStore
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.onStart
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import java.text.Normalizer
import javax.inject.Inject

enum class CompetitorSearchMode { NAME, START_NO, CHIP, CLASS, CLUB, VACANT }

private const val VACANT_SURNAME = "vacant"

private val diacriticsRegex = Regex("\\p{Mn}+")

/** Lowercases and strips diacritics so "mac", "māč" and "Mač" compare equal (search and sorting). */
internal fun normalizeForSearch(text: String): String =
    Normalizer.normalize(text, Normalizer.Form.NFD)
        .replace(diacriticsRegex, "")
        .lowercase()

data class ClassGroup(val classId: Int, val className: String, val runnerCount: Int)

data class ClubGroup(val clubId: Int, val clubName: String, val runnerCount: Int)

@HiltViewModel
class CompetitorSearchViewModel @Inject constructor(
    private val repository: StartListRepository,
    settingsDataStore: SettingsDataStore
) : ViewModel() {

    private val _mode = MutableStateFlow(CompetitorSearchMode.NAME)
    val mode: StateFlow<CompetitorSearchMode> = _mode.asStateFlow()

    private val _query = MutableStateFlow("")
    val query: StateFlow<String> = _query.asStateFlow()

    private val _selectedClass = MutableStateFlow<ClassGroup?>(null)
    val selectedClass: StateFlow<ClassGroup?> = _selectedClass.asStateFlow()

    private val _selectedClub = MutableStateFlow<ClubGroup?>(null)
    val selectedClub: StateFlow<ClubGroup?> = _selectedClub.asStateFlow()

    private val _selectedRunner = MutableStateFlow<RunnerEntity?>(null)
    val selectedRunner: StateFlow<RunnerEntity?> = _selectedRunner.asStateFlow()

    private val _currentTimeMs = MutableStateFlow(System.currentTimeMillis())
    val currentTimeMs: StateFlow<Long> = _currentTimeMs.asStateFlow()

    val settings: StateFlow<AppSettings> = settingsDataStore.settings
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), AppSettings.DEFAULT)

    /** Single shared observer of the runner table — reused by every derived flow below. */
    private val runnersFlow: StateFlow<List<RunnerEntity>> = repository.observeRunners()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    /** Runner paired with its precomputed normalized "name surname" — recomputed only when
     *  the runner set changes, so NAME search does not re-normalize on every keystroke. */
    private val normalizedRunners: StateFlow<List<Pair<RunnerEntity, String>>> = runnersFlow
        .map { list -> list.map { it to normalizeForSearch("${it.name} ${it.surname}") } }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    val classList: StateFlow<List<ClassGroup>> = runnersFlow
        .map { runners ->
            runners.groupBy { it.classId to it.className }
                .map { (key, group) -> ClassGroup(key.first, key.second, group.size) }
                .sortedBy { it.className.lowercase() }
        }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    val clubList: StateFlow<List<ClubGroup>> = runnersFlow
        .map { runners ->
            runners.groupBy { it.clubId to it.clubName }
                .map { (key, group) -> ClubGroup(key.first, key.second, group.size) }
                .sortedBy { it.clubName.lowercase() }
        }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    /** Query debounced so filtering runs once the user pauses, not on every keystroke. */
    @OptIn(FlowPreview::class)
    private val debouncedQuery: Flow<String> = _query
        .debounce(200)
        .onStart { emit(_query.value) }

    val results: StateFlow<List<RunnerEntity>> = combine(
        normalizedRunners,
        _mode,
        debouncedQuery,
        _selectedClass,
        _selectedClub
    ) { normalized, mode, query, selectedClass, selectedClub ->
        val term = query.trim()
        val runners = normalized.map { it.first }
        val filtered = when (mode) {
            CompetitorSearchMode.NAME ->
                if (term.isBlank()) {
                    emptyList()
                } else {
                    val needle = normalizeForSearch(term)
                    normalized.filter { it.second.contains(needle) }.map { it.first }
                }
            CompetitorSearchMode.START_NO ->
                if (term.isBlank()) emptyList()
                else runners.filter { it.startNumber.toString().contains(term) }
            CompetitorSearchMode.CHIP ->
                if (term.isBlank()) emptyList()
                else runners.filter { it.siCard.contains(term) }
            CompetitorSearchMode.CLASS ->
                if (selectedClass == null) emptyList()
                else runners.filter { it.classId == selectedClass.classId }
            CompetitorSearchMode.CLUB ->
                if (selectedClub == null) emptyList()
                else runners.filter { it.clubId == selectedClub.clubId }
            CompetitorSearchMode.VACANT ->
                runners.filter {
                    it.surname.trim().equals(VACANT_SURNAME, ignoreCase = true) &&
                        !it.className.startsWith("OPEN", ignoreCase = true) &&
                        !it.className.startsWith("DIR", ignoreCase = true)
                }
        }
        when (mode) {
            CompetitorSearchMode.CLASS -> filtered.sortedBy { it.startTime }
            CompetitorSearchMode.CLUB -> filtered.sortedBy { normalizeForSearch(it.surname) }
            CompetitorSearchMode.VACANT -> filtered.sortedBy { it.startTime }
            else -> filtered.sortedBy { it.startNumber }
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    val availableClasses: StateFlow<List<ClassEntry>> = combine(
        repository.observeLookupClasses(),
        repository.observeClasses()
    ) { lookupClasses, runnerClasses ->
        if (lookupClasses.isNotEmpty()) lookupClasses else runnerClasses
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    val availableClubs: StateFlow<List<ClubEntry>> = repository.observeLookupClubs()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    init {
        viewModelScope.launch {
            while (true) {
                _currentTimeMs.value = System.currentTimeMillis()
                delay(1000)
            }
        }
    }

    fun setMode(newMode: CompetitorSearchMode) {
        _mode.value = newMode
        _query.value = ""
        _selectedClass.value = null
        _selectedClub.value = null
    }

    fun updateQuery(value: String) { _query.value = value }

    fun selectClass(group: ClassGroup) { _selectedClass.value = group }

    fun selectClub(group: ClubGroup) { _selectedClub.value = group }

    fun clearGroupSelection() {
        _selectedClass.value = null
        _selectedClub.value = null
    }

    fun selectRunner(runner: RunnerEntity?) { _selectedRunner.value = runner }

    fun updateRunner(runner: RunnerEntity) {
        viewModelScope.launch {
            repository.updateRunner(runner)
            _selectedRunner.value = null
        }
    }
}

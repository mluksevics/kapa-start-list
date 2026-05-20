package com.orienteering.startref.ui.competitors

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.ArrowDropUp
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.ui.edituser.EditUserDialog
import com.orienteering.startref.ui.theme.ClassMenBlue
import com.orienteering.startref.ui.theme.ClassWomenYellow
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

private val rowTimeFormatter =
    DateTimeFormatter.ofPattern("HH:mm").withZone(ZoneId.systemDefault())

private val rowFontSize = 15.sp

private val colStartNo = 40.dp
private val colTime = 48.dp
private val colClass = 58.dp
private val colChip = 72.dp
private const val colNameWeight = 1.3f
private const val colClubWeight = 1f

private enum class SortColumn { START_NO, TIME, CLASS, CHIP, NAME, CLUB }

private data class ClassPartition(
    val priority: List<ClassGroup>,
    val men: List<ClassGroup>,
    val women: List<ClassGroup>,
    val other: List<ClassGroup>
)

private fun applySort(
    runners: List<RunnerEntity>,
    column: SortColumn?,
    ascending: Boolean
): List<RunnerEntity> {
    if (column == null) return runners
    val sorted = when (column) {
        SortColumn.START_NO -> runners.sortedBy { it.startNumber }
        SortColumn.TIME -> runners.sortedBy { it.startTime }
        SortColumn.CLASS -> runners.sortedBy { normalizeForSearch(it.className) }
        SortColumn.CHIP -> runners.sortedBy { it.siCard.toLongOrNull() ?: Long.MAX_VALUE }
        SortColumn.NAME -> runners.sortedBy { normalizeForSearch("${it.name} ${it.surname}") }
        SortColumn.CLUB -> runners.sortedBy { normalizeForSearch(it.clubName) }
    }
    return if (ascending) sorted else sorted.reversed()
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CompetitorSearchScreen(
    viewModel: CompetitorSearchViewModel = hiltViewModel()
) {
    val mode by viewModel.mode.collectAsStateWithLifecycle()
    val query by viewModel.query.collectAsStateWithLifecycle()
    val results by viewModel.results.collectAsStateWithLifecycle()
    val classList by viewModel.classList.collectAsStateWithLifecycle()
    val clubList by viewModel.clubList.collectAsStateWithLifecycle()
    val selectedClass by viewModel.selectedClass.collectAsStateWithLifecycle()
    val selectedClub by viewModel.selectedClub.collectAsStateWithLifecycle()
    val selectedRunner by viewModel.selectedRunner.collectAsStateWithLifecycle()
    val availableClasses by viewModel.availableClasses.collectAsStateWithLifecycle()
    val availableClubs by viewModel.availableClubs.collectAsStateWithLifecycle()

    // Header-tap sorting. Resets to the per-mode default order when the mode changes.
    var sortColumn by remember(mode) { mutableStateOf<SortColumn?>(null) }
    var sortAscending by remember(mode) { mutableStateOf(true) }
    val displayedResults = remember(results, sortColumn, sortAscending) {
        applySort(results, sortColumn, sortAscending)
    }
    val onSort: (SortColumn) -> Unit = { column ->
        if (sortColumn == column) {
            sortAscending = !sortAscending
        } else {
            sortColumn = column
            sortAscending = true
        }
    }

    val drilledInto = selectedClass?.className ?: selectedClub?.clubName

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(drilledInto ?: "Competitors") },
                navigationIcon = {
                    if (drilledInto != null) {
                        IconButton(onClick = { viewModel.clearGroupSelection() }) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    navigationIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
        ) {
            ModeSelector(
                selectedMode = mode,
                onModeSelected = { viewModel.setMode(it) }
            )
            HorizontalDivider()

            when (mode) {
                CompetitorSearchMode.NAME,
                CompetitorSearchMode.START_NO,
                CompetitorSearchMode.CHIP -> {
                    SearchField(
                        mode = mode,
                        query = query,
                        onQueryChange = { viewModel.updateQuery(it) }
                    )
                    Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                        when {
                            query.isBlank() -> CenteredHint(hintFor(mode))
                            results.isEmpty() -> CenteredHint("No matches")
                            else -> RunnerList(displayedResults, sortColumn, sortAscending, onSort) { viewModel.selectRunner(it) }
                        }
                    }
                }

                CompetitorSearchMode.CLASS -> {
                    Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                        if (selectedClass == null) {
                            if (classList.isEmpty()) {
                                CenteredHint("No competitors loaded")
                            } else {
                                ClassPicker(classList) { viewModel.selectClass(it) }
                            }
                        } else {
                            RunnerList(displayedResults, sortColumn, sortAscending, onSort) { viewModel.selectRunner(it) }
                        }
                    }
                }

                CompetitorSearchMode.CLUB -> {
                    Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                        if (selectedClub == null) {
                            if (clubList.isEmpty()) {
                                CenteredHint("No competitors loaded")
                            } else {
                                ClubPicker(clubList) { viewModel.selectClub(it) }
                            }
                        } else {
                            RunnerList(displayedResults, sortColumn, sortAscending, onSort) { viewModel.selectRunner(it) }
                        }
                    }
                }

                CompetitorSearchMode.VACANT -> {
                    Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                        if (results.isEmpty()) {
                            CenteredHint("No vacant slots")
                        } else {
                            RunnerList(displayedResults, sortColumn, sortAscending, onSort) { viewModel.selectRunner(it) }
                        }
                    }
                }
            }
        }
    }

    selectedRunner?.let { runner ->
        // Clock + settings collected here, not at screen level, so the per-second tick
        // recomposes only this dialog instead of the whole search screen.
        val currentTimeMs by viewModel.currentTimeMs.collectAsStateWithLifecycle()
        val settings by viewModel.settings.collectAsStateWithLifecycle()
        EditUserDialog(
            runner = runner,
            availableClasses = availableClasses,
            availableClubs = availableClubs,
            currentTimeMs = currentTimeMs - (settings.prestartMinutes * 60_000L),
            showQuickStartTimeButtons = false,
            onDismiss = { viewModel.selectRunner(null) },
            onSave = { updated -> viewModel.updateRunner(updated) }
        )
    }
}

private fun hintFor(mode: CompetitorSearchMode): String = when (mode) {
    CompetitorSearchMode.NAME -> "Type a name or surname to search"
    CompetitorSearchMode.START_NO -> "Type a start number to search"
    CompetitorSearchMode.CHIP -> "Type an SI chip number to search"
    else -> ""
}

@Composable
private fun ModeSelector(
    selectedMode: CompetitorSearchMode,
    onModeSelected: (CompetitorSearchMode) -> Unit
) {
    val modes = listOf(
        CompetitorSearchMode.NAME to "Name",
        CompetitorSearchMode.START_NO to "Start No",
        CompetitorSearchMode.CHIP to "Chip",
        CompetitorSearchMode.CLASS to "Class",
        CompetitorSearchMode.CLUB to "Club",
        CompetitorSearchMode.VACANT to "All Vacants"
    )
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .horizontalScroll(rememberScrollState())
            .padding(horizontal = 8.dp, vertical = 8.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        modes.forEach { (mode, label) ->
            FilterChip(
                selected = mode == selectedMode,
                onClick = { onModeSelected(mode) },
                label = { Text(label) }
            )
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SearchField(
    mode: CompetitorSearchMode,
    query: String,
    onQueryChange: (String) -> Unit
) {
    val placeholder = when (mode) {
        CompetitorSearchMode.NAME -> "Search by name"
        CompetitorSearchMode.START_NO -> "Search by start number"
        CompetitorSearchMode.CHIP -> "Search by SI chip"
        else -> ""
    }
    val keyboardType = when (mode) {
        CompetitorSearchMode.START_NO, CompetitorSearchMode.CHIP -> KeyboardType.Number
        else -> KeyboardType.Text
    }
    val focusRequester = remember { FocusRequester() }
    // Focus the input the moment a text mode is picked, so the cursor is ready.
    LaunchedEffect(mode) { focusRequester.requestFocus() }
    OutlinedTextField(
        value = query,
        onValueChange = onQueryChange,
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 12.dp, vertical = 8.dp)
            .focusRequester(focusRequester),
        placeholder = { Text(placeholder) },
        leadingIcon = { Icon(Icons.Default.Search, contentDescription = null) },
        trailingIcon = {
            if (query.isNotEmpty()) {
                IconButton(onClick = { onQueryChange("") }) {
                    Icon(Icons.Default.Close, contentDescription = "Clear")
                }
            }
        },
        singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = keyboardType)
    )
}

@Composable
private fun RunnerList(
    runners: List<RunnerEntity>,
    sortColumn: SortColumn?,
    sortAscending: Boolean,
    onSort: (SortColumn) -> Unit,
    onSelect: (RunnerEntity) -> Unit
) {
    Column(modifier = Modifier.fillMaxSize()) {
        HeaderRow(sortColumn, sortAscending, onSort)
        HorizontalDivider()
        LazyColumn(modifier = Modifier.fillMaxSize()) {
            items(runners, key = { it.startNumber }) { runner ->
                CompetitorRow(runner) { onSelect(runner) }
            }
        }
    }
}

@Composable
private fun HeaderRow(
    sortColumn: SortColumn?,
    ascending: Boolean,
    onSort: (SortColumn) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surfaceVariant)
            .padding(horizontal = 8.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        HeaderCell("No", SortColumn.START_NO, sortColumn, ascending, onSort, Modifier.width(colStartNo))
        HeaderCell("Time", SortColumn.TIME, sortColumn, ascending, onSort, Modifier.width(colTime))
        HeaderCell("Class", SortColumn.CLASS, sortColumn, ascending, onSort, Modifier.width(colClass))
        HeaderCell("Chip", SortColumn.CHIP, sortColumn, ascending, onSort, Modifier.width(colChip))
        HeaderCell("Name", SortColumn.NAME, sortColumn, ascending, onSort, Modifier.weight(colNameWeight))
        HeaderCell("Club", SortColumn.CLUB, sortColumn, ascending, onSort, Modifier.weight(colClubWeight))
    }
}

@Composable
private fun HeaderCell(
    label: String,
    column: SortColumn,
    activeColumn: SortColumn?,
    ascending: Boolean,
    onSort: (SortColumn) -> Unit,
    modifier: Modifier
) {
    val active = column == activeColumn
    val tint = if (active) MaterialTheme.colorScheme.primary
    else MaterialTheme.colorScheme.onSurfaceVariant
    Row(
        modifier = modifier.clickable { onSort(column) },
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = label,
            fontSize = 13.sp,
            fontWeight = FontWeight.Bold,
            color = tint,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f)
        )
        if (active) {
            Icon(
                imageVector = if (ascending) Icons.Default.ArrowDropUp else Icons.Default.ArrowDropDown,
                contentDescription = null,
                tint = tint,
                modifier = Modifier.size(16.dp)
            )
        }
    }
}

@Composable
private fun CompetitorRow(runner: RunnerEntity, onSelect: () -> Unit) {
    val bg = when {
        runner.className.startsWith("M", ignoreCase = true) -> ClassMenBlue
        runner.className.startsWith("W", ignoreCase = true) -> ClassWomenYellow
        else -> Color.White
    }
    val startTime =
        if (runner.startTime > 946_684_800_000L)
            rowTimeFormatter.format(Instant.ofEpochMilli(runner.startTime))
        else "--:--"
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bg)
            .clickable(onClick = onSelect)
            .padding(horizontal = 8.dp, vertical = 11.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Text(
            text = "${runner.startNumber}",
            fontSize = rowFontSize,
            fontWeight = FontWeight.Bold,
            maxLines = 1,
            modifier = Modifier.width(colStartNo)
        )
        Text(
            text = startTime,
            fontSize = rowFontSize,
            maxLines = 1,
            modifier = Modifier.width(colTime)
        )
        Text(
            text = runner.className,
            fontSize = rowFontSize,
            modifier = Modifier.width(colClass),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
        Text(
            text = runner.siCard,
            fontSize = rowFontSize,
            modifier = Modifier.width(colChip),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
        Text(
            text = "${runner.name} ${runner.surname}",
            fontSize = rowFontSize,
            modifier = Modifier.weight(colNameWeight),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
        Text(
            text = runner.clubName,
            fontSize = rowFontSize,
            modifier = Modifier.weight(colClubWeight),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
    HorizontalDivider()
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun ClassPicker(classes: List<ClassGroup>, onSelect: (ClassGroup) -> Unit) {
    val (priority, men, women, other) = remember(classes) {
        val priority = classes.filter {
            it.className.startsWith("DIR", ignoreCase = true) ||
                it.className.startsWith("OPEN", ignoreCase = true)
        }
        val rest = classes.filterNot { it in priority }
        val men = rest.filter { it.className.startsWith("M", ignoreCase = true) }
        val women = rest.filter { it.className.startsWith("W", ignoreCase = true) }
        val other = rest.filterNot { it in men || it in women }
        ClassPartition(priority, men, women, other)
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(8.dp)
    ) {
        if (priority.isNotEmpty()) {
            FlowRow(modifier = Modifier.fillMaxWidth()) {
                priority.forEach { group ->
                    PickerCell(
                        group.className,
                        group.runnerCount,
                        Modifier,
                        color = classGenderColor(group.className)
                    ) { onSelect(group) }
                }
            }
            HorizontalDivider(modifier = Modifier.padding(vertical = 8.dp))
        }
        Row(modifier = Modifier.fillMaxWidth()) {
            ClassGrid(men, columns = 2, modifier = Modifier.weight(1f), onSelect = onSelect)
            ClassGrid(women, columns = 2, modifier = Modifier.weight(1f), onSelect = onSelect)
        }
        if (other.isNotEmpty()) {
            HorizontalDivider(modifier = Modifier.padding(vertical = 8.dp))
            ClassGrid(other, columns = 4, modifier = Modifier.fillMaxWidth(), onSelect = onSelect)
        }
    }
}

@Composable
private fun ClassGrid(
    classes: List<ClassGroup>,
    columns: Int,
    modifier: Modifier = Modifier,
    onSelect: (ClassGroup) -> Unit
) {
    Column(modifier = modifier) {
        classes.chunked(columns).forEach { rowItems ->
            Row(modifier = Modifier.fillMaxWidth()) {
                rowItems.forEach { group ->
                    PickerCell(
                        group.className,
                        group.runnerCount,
                        Modifier.weight(1f),
                        color = classGenderColor(group.className)
                    ) { onSelect(group) }
                }
                repeat(columns - rowItems.size) {
                    Spacer(modifier = Modifier.weight(1f))
                }
            }
        }
    }
}

@Composable
private fun ClubPicker(clubs: List<ClubGroup>, onSelect: (ClubGroup) -> Unit) {
    LazyVerticalGrid(
        columns = GridCells.Fixed(3),
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(8.dp)
    ) {
        items(clubs, key = { "club_${it.clubId}_${it.clubName}" }) { group ->
            PickerCell(group.clubName, group.runnerCount, Modifier.fillMaxWidth()) {
                onSelect(group)
            }
        }
    }
}

@Composable
private fun classGenderColor(className: String): Color = when {
    className.startsWith("M", ignoreCase = true) -> ClassMenBlue
    className.startsWith("W", ignoreCase = true) -> ClassWomenYellow
    else -> MaterialTheme.colorScheme.surfaceVariant
}

@Composable
private fun PickerCell(
    label: String,
    count: Int,
    modifier: Modifier = Modifier,
    color: Color = MaterialTheme.colorScheme.surfaceVariant,
    onClick: () -> Unit
) {
    Surface(
        onClick = onClick,
        modifier = modifier.padding(4.dp),
        shape = RoundedCornerShape(8.dp),
        color = color
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Text(
                text = if (count == 1) "1 runner" else "$count runners",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1
            )
        }
    }
}

@Composable
private fun CenteredHint(text: String) {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(32.dp)
        )
    }
}

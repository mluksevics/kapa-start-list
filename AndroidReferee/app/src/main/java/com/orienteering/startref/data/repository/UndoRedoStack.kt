package com.orienteering.startref.data.repository

import com.orienteering.startref.data.local.entity.RunnerEntity
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject
import javax.inject.Singleton

data class RunnerOperation(val before: RunnerEntity, val after: RunnerEntity)

@Singleton
class UndoRedoStack @Inject constructor() {
    private val _undoStack = MutableStateFlow<List<RunnerOperation>>(emptyList())
    private val _redoStack = MutableStateFlow<List<RunnerOperation>>(emptyList())

    val canUndo: StateFlow<Boolean> get() = _canUndo
    val canRedo: StateFlow<Boolean> get() = _canRedo

    private val _canUndo = MutableStateFlow(false)
    private val _canRedo = MutableStateFlow(false)

    fun record(before: RunnerEntity, after: RunnerEntity) {
        _undoStack.value = (_undoStack.value + RunnerOperation(before, after)).takeLast(5)
        _redoStack.value = emptyList()
        updateFlags()
    }

    /** Returns the entity to restore (before state), or null if stack empty. */
    fun popUndo(): RunnerEntity? {
        val stack = _undoStack.value.ifEmpty { return null }
        val op = stack.last()
        _undoStack.value = stack.dropLast(1)
        _redoStack.value = (_redoStack.value + op).takeLast(5)
        updateFlags()
        return op.before
    }

    /** Returns the entity to restore (after state), or null if stack empty. */
    fun popRedo(): RunnerEntity? {
        val stack = _redoStack.value.ifEmpty { return null }
        val op = stack.last()
        _redoStack.value = stack.dropLast(1)
        _undoStack.value = (_undoStack.value + op).takeLast(5)
        updateFlags()
        return op.after
    }

    private fun updateFlags() {
        _canUndo.value = _undoStack.value.isNotEmpty()
        _canRedo.value = _redoStack.value.isNotEmpty()
    }
}

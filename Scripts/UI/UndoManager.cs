using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoasterForge.UI {
    public class UndoManager : MonoBehaviour {
        private Stack<string> _undoStack = new();
        private Stack<string> _redoStack = new();
        private Action<string> _applyState;
        private Func<string> _getState;

        public static UndoManager Instance { get; private set; }

        private void Awake() {
            Instance = this;
        }

        private void Update() {
            if (Keyboard.current.ctrlKey.isPressed || Keyboard.current.leftCommandKey.isPressed) {
                if (Keyboard.current.zKey.wasPressedThisFrame) {
                    if (Keyboard.current.shiftKey.isPressed) {
                        Redo();
                    }
                    else {
                        Undo();
                    }
                }
                else if (Keyboard.current.yKey.wasPressedThisFrame) {
                    Redo();
                }
            }
        }

        public static void Initialize(Action<string> applyState, Func<string> getState) {
            Instance._applyState = applyState;
            Instance._getState = getState;
        }

        public static void Record() {
            var state = Instance._getState();
            Instance._undoStack.Push(state);
            Instance._redoStack.Clear();
        }

        public static void Undo() {
            if (Instance._undoStack.Count == 0) return;
            var current = Instance._getState();
            var prev = Instance._undoStack.Pop();
            Instance._redoStack.Push(current);
            Instance._applyState(prev);
        }

        public static void Redo() {
            if (Instance._redoStack.Count == 0) return;
            var current = Instance._getState();
            var next = Instance._redoStack.Pop();
            Instance._undoStack.Push(current);
            Instance._applyState(next);
        }

        public static void Clear() {
            Instance._undoStack.Clear();
            Instance._redoStack.Clear();
        }
    }
}

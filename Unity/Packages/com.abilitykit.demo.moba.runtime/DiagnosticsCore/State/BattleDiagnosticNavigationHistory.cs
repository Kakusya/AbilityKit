using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public sealed class BattleDiagnosticNavigationHistory
    {
        public const int DefaultCapacity = 100;

        private readonly List<BattleDiagnosticSelection> _entries;
        private BattleDiagnosticSessionScope _scope;
        private int _currentIndex;

        public BattleDiagnosticNavigationHistory(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            _entries = new List<BattleDiagnosticSelection>(capacity);
            _currentIndex = -1;
        }

        public int Capacity { get; }
        public int Count => _entries.Count;
        public bool CanGoBack => _currentIndex > 0;
        public bool CanGoForward => _currentIndex >= 0 && _currentIndex < _entries.Count - 1;

        public BattleDiagnosticSessionScope Scope => _scope;

        public BattleDiagnosticSelection Current =>
            _currentIndex >= 0 && _currentIndex < _entries.Count
                ? _entries[_currentIndex]
                : default;

        public void Reset(BattleDiagnosticSessionScope scope)
        {
            _scope = scope;
            _entries.Clear();
            _currentIndex = -1;
        }

        public bool NavigateTo(BattleDiagnosticSelection selection)
        {
            if (!selection.IsValid)
            {
                return false;
            }

            if (!_scope.IsValid)
            {
                _scope = selection.Scope;
            }
            else if (selection.Scope != _scope)
            {
                Reset(selection.Scope);
            }

            if (Current == selection)
            {
                return false;
            }

            RemoveForwardEntries();
            _entries.Add(selection);
            _currentIndex = _entries.Count - 1;
            TrimToCapacity();
            return true;
        }

        public bool TryGoBack(out BattleDiagnosticSelection selection)
        {
            if (!CanGoBack)
            {
                selection = default;
                return false;
            }

            _currentIndex--;
            selection = _entries[_currentIndex];
            return true;
        }

        public bool TryGoForward(out BattleDiagnosticSelection selection)
        {
            if (!CanGoForward)
            {
                selection = default;
                return false;
            }

            _currentIndex++;
            selection = _entries[_currentIndex];
            return true;
        }

        public bool TryGetEntry(int index, out BattleDiagnosticSelection selection)
        {
            if (index < 0 || index >= _entries.Count)
            {
                selection = default;
                return false;
            }

            selection = _entries[index];
            return true;
        }

        private void RemoveForwardEntries()
        {
            var firstForwardIndex = _currentIndex + 1;
            if (firstForwardIndex < _entries.Count)
            {
                _entries.RemoveRange(firstForwardIndex, _entries.Count - firstForwardIndex);
            }
        }

        private void TrimToCapacity()
        {
            var overflow = _entries.Count - Capacity;
            if (overflow <= 0)
            {
                return;
            }

            _entries.RemoveRange(0, overflow);
            _currentIndex -= overflow;
        }
    }
}

using System;
using System.Windows.Input;
using System.Threading.Tasks;

namespace SwitchManager.Commands
{
    public class RelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _execute;
        private readonly Predicate<T> _canExecute;
        private bool _isExecuting;

        public RelayCommand(Func<T, Task> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
            : this(p => { execute(p); return Task.CompletedTask; }, canExecute)
        {
        }

        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
            {
                return false;
            }

            return _canExecute == null || _canExecute((T)parameter!);
        }

        public async void Execute(object? parameter)
        {
            await ExecuteAsync((T)parameter!);
        }

        public async Task ExecuteAsync(T parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            try
            {
                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
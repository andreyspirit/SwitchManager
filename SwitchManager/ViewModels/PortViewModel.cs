using System.Windows.Input;
using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class PortViewModel : ViewModelBase
    {
        private readonly PortEntry _model;
        private bool _isActive;

        public PortViewModel(PortEntry model, ICommand toggleCommand)
        {
            _model = model;
            ToggleCommand = toggleCommand;
        }

        // Port number for serial commands
        public int Number => _model.Number;

        // Display name on the button (e.g., "REAL")
        public string Alias => _model.Alias;

        // Role: Source or Target (used for UI filtering)
        public PortType Type => _model.Type;

        // Tracks whether the port is currently "no shutdown" (Green) or "shutdown" (Orange)
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        // Action when the button is clicked
        public ICommand ToggleCommand { get; }
    }
}
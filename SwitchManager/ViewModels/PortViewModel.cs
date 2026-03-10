using SwitchManager.Models;
using SwitchManager.ViewModels;
using System.Windows.Input;

public class PortViewModel : ViewModelBase
{
    private readonly PortEntry _model;
    private bool _isActive;

    // We store the VlanId here, passed from the group
    public int VlanId { get; }

    public PortViewModel(PortEntry model, ICommand toggleCommand, int vlanId)
    {
        _model = model;
        ToggleCommand = toggleCommand;
        VlanId = vlanId; // Set the VLAN ID from the parent group
    }

    public int Number => _model.Number;
    public string Alias => _model.Alias ?? "Unknown";
    public PortType Type => _model.Type;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public ICommand ToggleCommand { get; }
}
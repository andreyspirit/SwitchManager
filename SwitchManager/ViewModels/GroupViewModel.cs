using System.Collections.ObjectModel;
using System.Windows.Input;
using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class GroupViewModel
    {
        public string GroupName { get; }
        public int VlanId { get; }

        // Collection of ports to be displayed in the UI
        public ObservableCollection<PortViewModel> Ports { get; } = new();

        public GroupViewModel(PortGroup group, ICommand toggleCommand)
        {
            GroupName = group.GroupName;
            VlanId = group.VlanId; // Assuming PortGroup model has VlanId

            foreach (var p in group.Ports)
            {
                // Pass VlanId to each PortViewModel
                Ports.Add(new PortViewModel(p, toggleCommand, VlanId));
            }
        }
    }
}
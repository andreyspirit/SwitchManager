using System.Collections.ObjectModel;
using System.Windows.Input;
using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class GroupViewModel
    {
        public string GroupName { get; }
        public int VlanId { get; }
        public ObservableCollection<PortViewModel> Ports { get; } = new();

        public GroupViewModel(PortGroup group, ICommand toggleCommand)
        {
            GroupName = group.GroupName;
            VlanId = group.VlanId;
            foreach (var p in group.Ports)
                Ports.Add(new PortViewModel(p, toggleCommand));
        }
    }
}
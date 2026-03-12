using System.Collections.ObjectModel;
using System.Linq;
using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class GroupViewModel : ViewModelBase
    {
        public string GroupName { get; }
        public int VlanId { get; }
        public int TargetPortNumber { get; }
        public ObservableCollection<PortViewModel> Ports { get; } = new();

        private bool _isTargetLinkActive = true;
        public bool IsTargetLinkActive
        {
            get => _isTargetLinkActive;
            set => SetProperty(ref _isTargetLinkActive, value);
        }

        public GroupViewModel(PortGroup group)
        {
            GroupName = group.GroupName;
            VlanId = group.VlanId;
            IsTargetLinkActive = false;

            // 1. Extract the port number designated as "Target" in the JSON
            var target = group.Ports.FirstOrDefault(p => p.Type == PortType.Target);
            TargetPortNumber = target?.Number ?? 0;

            // 2. Filter and add only "Source" ports (e.g., REAL, SIM) to the button collection
            // This prevents empty buttons for the Target port itself
            var sourcePorts = group.Ports.Where(p => p.Type == PortType.Source);
            foreach (var p in sourcePorts)
            {
                var portVm = new PortViewModel(p);
                portVm.TargetVlanId = VlanId;
                Ports.Add(portVm);
            }           
        }
    }
}
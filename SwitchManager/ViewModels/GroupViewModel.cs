using System.Collections.ObjectModel;
using System.Linq;
using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class GroupViewModel : ViewModelBase
    {
        private string _targetPortStatus;
        private bool _existsOnHardware;

        public string GroupName { get; }
        public int VlanId { get; }
        public int TargetPortNumber { get; }

        public bool ExistsOnHardware
        {
            get => _existsOnHardware;
            set => SetProperty(ref _existsOnHardware, value);
        }

        public string TargetPortStatus
        {
            get => _targetPortStatus;
            set => SetProperty(ref _targetPortStatus, value);
        }

        public ObservableCollection<PortViewModel> Ports { get; }

        public GroupViewModel(PortGroup group)
        {
            Ports = new ObservableCollection<PortViewModel>();
            GroupName = group.GroupName;
            VlanId = group.VlanId;

            // 1. Находим целевой порт (Target)
            var target = group.Ports.FirstOrDefault(p => p.Type == PortType.Target);
            TargetPortNumber = target?.Number ?? 0;

            // 2. Добавляем только порты-источники (Source) в коллекцию кнопок
            var sourcePorts = group.Ports.Where(p => p.Type == PortType.Source);
            foreach (var p in sourcePorts)
            {
                var portVm = new PortViewModel(p)
                {
                    TargetVlanId = VlanId,
                };
                Ports.Add(portVm);
            }
        }
    }
}
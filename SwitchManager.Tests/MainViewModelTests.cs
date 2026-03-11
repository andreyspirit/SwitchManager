using Moq;
using SwitchManager.Services;
using SwitchManager.ViewModels;
using Xunit;
using System.Linq;
using System.Threading.Tasks;

namespace SwitchManager.Tests
{
    public class MainViewModelTests
    {
        private readonly Mock<ISerialService> _serialMock;
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            _serialMock = new Mock<ISerialService>();
            _serialMock.Setup(s => s.IsConnected).Returns(true);

            // Initialize the ViewModel.
            // Note: InitializeApp runs in the constructor, so ensure 
            // the environment/config is predictable for testing.
            _viewModel = new MainViewModel(_serialMock.Object);
        }

        [Fact]
        public async Task TogglePort_ShouldActivateSelectedPort_AndCallHardwareAsync()
        {
            // Arrange
            var group = _viewModel.PortGroups.First();
            var targetPort = group.Ports.First(p => p.Type == Models.PortType.Source);
            targetPort.IsActive = false;
            targetPort.FullInterfaceName = "Gi1/0/1"; // Simulate that the hardware audit has completed

            // Act
            // In WPF, command execution is often async void. 
            // We call Execute and wait briefly to let the Task complete.
            _viewModel.ToggleCommand.Execute(targetPort);

            // Allow a small delay for the asynchronous task to finish
            await Task.Delay(100);

            // Assert
            Assert.True(targetPort.IsActive);

            // Verify the call to the new SetPortVlanAsync method
            _serialMock.Verify(s => s.SetPortVlanAsync(
                It.Is<string>(n => n == "Gi1/0/1"),
                It.Is<int>(v => v == targetPort.TargetVlanId)
            ), Times.Once);
        }

        [Fact]
        public async Task TogglePort_ShouldDeactivateOtherPortsInSameGroup_ExclusiveLogic()
        {
            // Arrange
            var group = _viewModel.PortGroups.First();
            var realPort = group.Ports[0];
            var simPort = group.Ports[1];

            simPort.IsActive = true;
            realPort.IsActive = false;

            // Act
            _viewModel.ToggleCommand.Execute(realPort);
            await Task.Delay(100);

            // Assert
            Assert.True(realPort.IsActive, "REAL port must be Active.");
            Assert.False(simPort.IsActive, "SIM port must be Deactivated (Exclusive Logic).");
        }

        [Fact]
        public async Task CommandExecution_ShouldUpdateStatusMessage_OnFailure()
        {
            // Arrange
            var group = _viewModel.PortGroups.First();
            var port = group.Ports.First();
            port.FullInterfaceName = "Gi1/0/1";

            // Configure Mock to throw an exception during an asynchronous call
            _serialMock.Setup(s => s.SetPortVlanAsync(It.IsAny<string>(), It.IsAny<int>()))
                       .ThrowsAsync(new System.IO.IOException("COM Port Lost"));

            // Act
            _viewModel.ToggleCommand.Execute(port);
            await Task.Delay(100);

            // Assert
            // Check if ViewModel handles the exception and updates the UI status
            Assert.Contains("Error", _viewModel.StatusMessage);
            Assert.Contains("COM Port Lost", _viewModel.StatusMessage);
        }
    }
}
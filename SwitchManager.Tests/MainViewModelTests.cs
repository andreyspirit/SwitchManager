using Moq;
using Xunit;
using SwitchManager.ViewModels;
using SwitchManager.Services;
using SwitchManager.Models;

public class MainViewModelTests
{
    [UIFact]
    public async Task AuditHardwareAsync_UpdatesPortStatus_WhenDataIsValid()
    {
        // 1. Arrange
        var mockService = new Mock<ISerialService>();

        // Mocking the 'show interface status' output
        // Gi0/1 is the Source port defined in our test setup
        string fakeCiscoOutput = "Gi0/1    SourcePort     connected    10\n" +
                                 "Gi0/24   TargetPort     connected    10";

        mockService.Setup(s => s.IsConnected).Returns(true);
        mockService.Setup(s => s.GetHardwareStatusAsync()).ReturnsAsync(fakeCiscoOutput);

        var viewModel = new MainViewModel(mockService.Object);

        // 2. Setup Data using your PortEntry model and PortViewModel constructor
        var portEntry = new PortEntry
        {
            Number = 1,
            Alias = "Simulator Port",
            Type = PortType.Source
        };

        var portVm = new PortViewModel(portEntry)
        {
            TargetVlanId = 10 // Set the expected VLAN for comparison logic
        };

        // 1. Setup the Group via its model
        var groupModel = new PortGroup { GroupName = "Test", VlanId = 10, Ports = new List<PortEntry>() };
        var testGroup = new GroupViewModel(groupModel);

        // 2. Add the PortViewModel to the Group
        testGroup.Ports.Add(portVm);

        // 3. Add the Group to the MainViewModel (No '=' sign!)
        viewModel.PortGroups.Clear();
        viewModel.PortGroups.Add(testGroup);

        // 3. Act
        await viewModel.AuditHardwareAsync();

        // 4. Assert
        // Check if the Source Port correctly parsed the "connected" status
        Assert.True(portVm.ExistsOnHardware);
        Assert.Equal("connected", portVm.PortStatus);

        // IsActive should be true because parsed VLAN (10) matches TargetVlanId (10)
        Assert.True(portVm.IsActive);

        mockService.Verify(s => s.GetHardwareStatusAsync(), Times.Exactly(2));
    }

    [UIFact]
    public async Task AuditHardwareAsync_HandlesHardwareException_Gracefully()
    {
        // 1. Arrange
        var mockService = new Mock<ISerialService>();

        mockService.Setup(s => s.GetHardwareStatusAsync())
                   .ThrowsAsync(new Exception("Switch did not respond to status command."));

        mockService.Setup(s => s.IsConnected).Returns(true);

        var viewModel = new MainViewModel(mockService.Object);

        var group = new GroupViewModel(new PortGroup { GroupName = "Test", Ports = new List<PortEntry>() });
        viewModel.PortGroups.Add(group);

        // 2. Act
        await viewModel.AuditHardwareAsync();

        // 3. Assert
        Assert.Contains("error", viewModel.StatusMessage);

        Assert.False(group.ExistsOnHardware);
    }

    [UIFact]
    public async Task ExecuteAuditAsync_ResetsIsBusy_EvenOnException()
    {
        // Arrange
        var mockService = new Mock<ISerialService>();
        // Simulate a hardware disconnection exception
        mockService.Setup(s => s.GetHardwareStatusAsync()).ThrowsAsync(new System.IO.IOException("Port closed"));
        mockService.Setup(s => s.IsConnected).Returns(true);

        var viewModel = new MainViewModel(mockService.Object);

        // Act
        // We call the Command version to test the try/finally logic
        await viewModel.AuditCommand.ExecuteAsync(null);

        // Assert
        Assert.False(viewModel.IsBusy); // UI must be unlocked
        Assert.Contains("Error", viewModel.StatusMessage);
    }
}
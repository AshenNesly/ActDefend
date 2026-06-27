using System;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.GUI;
using FluentAssertions;
using Moq;
using Xunit;

namespace ActDefend.UnitTests.GUI;

public class SettingsViewModelTests
{
    private readonly Mock<IConfigurationManager> _configManagerMock;
    private readonly ActDefendOptions _defaultOptions;

    public SettingsViewModelTests()
    {
        _configManagerMock = new Mock<IConfigurationManager>();
        _defaultOptions = new ActDefendOptions();
        
        // Ensure defaults are populated
        ConfigurationProfileHelper.ApplyProfile(_defaultOptions, ConfigurationProfile.Balanced);
        
        _configManagerMock.Setup(m => m.CurrentOptions).Returns(_defaultOptions);
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(_configManagerMock.Object);
    }

    [Fact]
    public void Validation_ValidValues_ProducesNoError()
    {
        var vm = CreateViewModel();
        
        // Balanced defaults should be valid
        vm.Validate();
        vm.ValidationErrorMessage.Should().BeEmpty();
    }

    [Theory]
    [InlineData(39)]
    [InlineData(91)]
    public void Validation_InvalidSuspicionThreshold_ProducesError(double value)
    {
        var vm = CreateViewModel();
        vm.Stage1SuspicionThreshold = value;
        
        vm.ValidationErrorMessage.Should().Contain("SuspicionThreshold must be between 40 and 90");
    }

    [Fact]
    public void Validation_TotalStage1WeightsExceeds100_ProducesError()
    {
        var vm = CreateViewModel();
        // Sum will be 6*50 = 300
        vm.Stage1WeightWriteRate = 50;
        vm.Stage1WeightUniqueFilesWritten = 50;
        vm.Stage1WeightRenameRate = 50;
        vm.Stage1WeightDirectorySpread = 50;
        vm.Stage1WeightWriteReadRatio = 50;
        vm.Stage1WeightPreExistingModifyRate = 50;

        vm.ValidationErrorMessage.Should().Contain("Total Stage 1 weights cannot exceed 100");
    }

    [Fact]
    public void Validation_ContextWindowLessThanPrimary_ProducesError()
    {
        var vm = CreateViewModel();
        vm.FeaturesPrimaryWindowSeconds = 10;
        vm.FeaturesContextWindowSeconds = 5; // Invalid: must be >= Primary

        vm.ValidationErrorMessage.Should().Contain("ContextWindowSeconds must be >= PrimaryWindowSeconds");
    }

    [Theory]
    [InlineData(1023)]
    [InlineData(100001)]
    public void Validation_InvalidEventQueueCapacity_ProducesError(int value)
    {
        var vm = CreateViewModel();
        vm.CollectorEventQueueCapacity = value;

        vm.ValidationErrorMessage.Should().Contain("EventQueueCapacity must be between 1024 and 100000");
    }

    [Fact]
    public void ProfileSelection_UpdatesPropertiesAndDescription()
    {
        var vm = CreateViewModel();
        
        vm.ApplyProfileCommand.Execute("Sensitive");

        vm.SelectedProfile.Should().Be(ConfigurationProfile.Sensitive);
        vm.Stage1SuspicionThreshold.Should().Be(45.0);
        vm.CurrentProfileDescription.Should().Contain("Detects earlier");
    }
}

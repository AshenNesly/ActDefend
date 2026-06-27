using System;
using ActDefend.Core.Configuration;
using Xunit;
using FluentAssertions;

namespace ActDefend.UnitTests.Configuration;

public class ConfigurationProfileHelperTests
{
    [Fact]
    public void ApplyProfile_Balanced_SetsExpectedDefaults()
    {
        // Arrange
        var options = new ActDefendOptions();

        // Act
        ConfigurationProfileHelper.ApplyProfile(options, ConfigurationProfile.Balanced);

        // Assert
        options.Stage1.SuspicionThreshold.Should().Be(60.0);
        options.Stage1.Weights.PreExistingModifyRate.Should().Be(25.0);
        options.Stage2.EntropyThreshold.Should().Be(7.2);
        options.Features.PrimaryWindowSeconds.Should().Be(5);
        options.Collector.EventQueueCapacity.Should().Be(4096);
    }

    [Fact]
    public void ApplyProfile_Sensitive_IncreasesSensitivity()
    {
        var options = new ActDefendOptions();
        ConfigurationProfileHelper.ApplyProfile(options, ConfigurationProfile.Sensitive);

        options.Stage1.SuspicionThreshold.Should().Be(45.0);
        options.Stage1.Weights.PreExistingModifyRate.Should().Be(30.0);
        options.Stage2.EntropyThreshold.Should().Be(7.0);
    }

    [Fact]
    public void ApplyProfile_Conservative_DecreasesSensitivity()
    {
        var options = new ActDefendOptions();
        ConfigurationProfileHelper.ApplyProfile(options, ConfigurationProfile.Conservative);

        options.Stage1.SuspicionThreshold.Should().Be(75.0);
        options.Stage1.Weights.PreExistingModifyRate.Should().Be(40.0);
        options.Stage2.EntropyThreshold.Should().Be(7.6);
    }

    [Fact]
    public void ApplyProfile_LowResource_ReducesPressure()
    {
        var options = new ActDefendOptions();
        ConfigurationProfileHelper.ApplyProfile(options, ConfigurationProfile.LowResource);

        options.Features.PrimaryWindowSeconds.Should().Be(10);
        options.Features.ContextWindowSeconds.Should().Be(30);
        options.Collector.EventQueueCapacity.Should().Be(1024);
    }

    [Fact]
    public void ApplyProfile_Custom_DoesNotModifyOptions()
    {
        var options = new ActDefendOptions();
        options.Stage1.SuspicionThreshold = 99.9;

        ConfigurationProfileHelper.ApplyProfile(options, ConfigurationProfile.Custom);

        options.Stage1.SuspicionThreshold.Should().Be(99.9);
    }
}

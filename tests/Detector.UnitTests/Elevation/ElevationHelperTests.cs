using ActDefend.Core.Elevation;
using FluentAssertions;
using Xunit;

namespace ActDefend.UnitTests.Elevation;

/// <summary>
/// Unit tests for the ElevationHelper utility.
/// These tests validate the helper's logic without requiring actual UAC interaction.
/// </summary>
public sealed class ElevationHelperTests
{
    [Fact]
    public void IsElevated_ReturnsBoolean_WithoutThrowing()
    {
        // Arrange / Act
        var act = () => ElevationHelper.IsElevated();

        // Assert — should not throw regardless of elevation state in CI
        act.Should().NotThrow();
    }

    [Fact]
    public void IsElevated_ReturnsConsistentResult_WhenCalledMultipleTimes()
    {
        // Elevation state doesn't change mid-process.
        var first  = ElevationHelper.IsElevated();
        var second = ElevationHelper.IsElevated();

        first.Should().Be(second);
    }
}

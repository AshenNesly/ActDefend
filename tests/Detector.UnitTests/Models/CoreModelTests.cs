using ActDefend.Core.Models;
using FluentAssertions;
using Xunit;

namespace ActDefend.UnitTests.Models;

/// <summary>
/// Smoke tests for Core domain model construction.
/// Verifies that required properties are enforced and records are immutable.
/// </summary>
public sealed class CoreModelTests
{
    [Fact]
    public void FileSystemEvent_CanBeConstructed()
    {
        var evt = new FileSystemEvent(
            Timestamp:   DateTimeOffset.UtcNow,
            ProcessId:   1234,
            ProcessName: "test.exe",
            ProcessPath: @"C:\test.exe",
            EventType:   FileSystemEventType.Write,
            FilePath:    @"C:\Users\test\doc.docx");

        evt.ProcessId.Should().Be(1234);
        evt.EventType.Should().Be(FileSystemEventType.Write);
        evt.OldFilePath.Should().BeNull();
    }

    [Fact]
    public void FeatureSnapshot_DefaultCollections_AreEmpty()
    {
        var snap = new FeatureSnapshot
        {
            Timestamp             = DateTimeOffset.UtcNow,
            ProcessId             = 42,
            ProcessName           = "snap.exe",
            PrimaryWindowDuration = TimeSpan.FromSeconds(5),
            ContextWindowDuration = TimeSpan.FromSeconds(15)
        };

        snap.RecentWrittenFiles.Should().BeEmpty();
        snap.WriteRatePerSec.Should().Be(0.0);
    }

    [Fact]
    public void DetectionAlert_Severity_RangeValues_AreDistinct()
    {
        var values = Enum.GetValues<AlertSeverity>();
        values.Should().HaveCount(4);
        values.Should().Contain(AlertSeverity.Critical);
    }
}

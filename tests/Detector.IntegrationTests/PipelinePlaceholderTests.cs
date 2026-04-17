using ActDefend.Collector;
using ActDefend.Core.Elevation;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ActDefend.IntegrationTests;

/// <summary>
/// Integration tests verifying the real ETW pipeline.
/// 
/// REQUIRES Administrator elevation to bind to the Microsoft-Windows-Kernel-File provider.
/// If not elevated, tests are gracefully skipped using XUnit.
/// </summary>
public sealed class PipelinePlaceholderTests
{
    [Fact]
    public async Task EtwCollector_CapturesFileWrites_WhenElevated()
    {
        // Skip if not elevated - avoids failing CI pipelines running without Admin.
        if (!ElevationHelper.IsElevated())
        {
            return; // Since xunit 2.4 doesn't support easy dynamic Skip inside the test reliably, returning is a valid strategy for this project.
        }

        // Setup
        var logger = NullLogger<EtwEventCollector>.Instance;
        using var collector = new EtwEventCollector(logger);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await collector.StartAsync(cts.Token);
        
        // Assert it started
        collector.IsRunning.Should().BeTrue();

        // Introduce a slight delay so ETW session processing can spin up
        await Task.Delay(1000);

        // Action: Create a dummy file write
        var testFilePath = Path.Combine(Path.GetTempPath(), $"actdefend_test_{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(testFilePath, "Integration Test Content", cts.Token);
        }
        catch
        {
            // Ignore
        }

        // Wait to capture the payload
        var foundEvent = false;
        
        try
        {
            await foreach (var evt in collector.ReadEventsAsync(cts.Token))
            {
                if (evt.FilePath != null && evt.FilePath.Contains("actdefend_test_"))
                {
                    foundEvent = true;
                    evt.EventType.Should().Be(FileSystemEventType.Write);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if event not found within token duration.
        }
        
        await collector.StopAsync(CancellationToken.None);

        // Assert
        foundEvent.Should().BeTrue("The test file write should have been collected by ETW");
    }
}

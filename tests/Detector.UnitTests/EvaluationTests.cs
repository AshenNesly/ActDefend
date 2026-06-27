using System;
using System.Collections.Generic;
using System.IO;
using ActDefend.Evaluation;
using FluentAssertions;
using Xunit;

namespace ActDefend.UnitTests;

public class EvaluationTests : IDisposable
{
    private readonly string _tempDir;

    public EvaluationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ActDefend_EvaluationTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void GenerateCsv_ShouldCreateValidCsv()
    {
        // Arrange
        var results = new List<EvaluationResult>
        {
            new EvaluationResult
            {
                ScenarioName = "TestScenario",
                WorkloadType = WorkloadType.Ransomware,
                FileCount = 10,
                DelayMs = 0,
                DirectoryDepth = 1,
                Pass = true,
                FailureReason = "",
                AlertRaised = true,
                AlertCount = 1,
                DetectionLatencyMs = 150.5,
                InternalDetectorLatencyMs = 150.5,
                AverageCpuUsagePercent = 12.3,
                PeakCpuUsagePercent = 25.0,
                AverageMemoryMb = 50.5,
                PeakMemoryMb = 60.0,
                EventsProcessed = 100,
                EventsDropped = 0,
                SuspicionScore = 85.0,
                HighEntropyFileCount = 10
            }
        };

        var outputPath = Path.Combine(_tempDir, "test.csv");

        // Act
        ReportGenerator.GenerateCsv(results, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var lines = File.ReadAllLines(outputPath);
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("ScenarioName,WorkloadType");
        lines[1].Should().StartWith("TestScenario,Ransomware,10,0,1,True,,True,1,150.50,150.50,12.30,25.00,50.50,60.00,100,0,85.00,10");
    }

    [Fact]
    public void GenerateJson_ShouldCreateValidJson()
    {
        // Arrange
        var results = new List<EvaluationResult>
        {
            new EvaluationResult
            {
                ScenarioName = "TestJson",
                Pass = true
            }
        };

        var outputPath = Path.Combine(_tempDir, "test.json");

        // Act
        ReportGenerator.GenerateJson(results, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var json = File.ReadAllText(outputPath);
        json.Should().Contain("\"ScenarioName\": \"TestJson\"");
        json.Should().Contain("\"Pass\": true");
    }

    [Fact]
    public void GenerateMarkdownSummary_ShouldCreateValidMarkdown()
    {
        // Arrange
        var results = new List<EvaluationResult>
        {
            new EvaluationResult
            {
                ScenarioName = "RansomwareFast",
                WorkloadType = WorkloadType.Ransomware,
                Pass = true,
                AlertRaised = true,
                DetectionLatencyMs = 200,
                AverageCpuUsagePercent = 5,
                PeakMemoryMb = 100
            },
            new EvaluationResult
            {
                ScenarioName = "BenignTest",
                WorkloadType = WorkloadType.Benign,
                Pass = true,
                AlertRaised = false
            }
        };

        var outputPath = Path.Combine(_tempDir, "test_summary.md");

        // Act
        ReportGenerator.GenerateMarkdownSummary(results, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var md = File.ReadAllText(outputPath);
        md.Should().Contain("# ActDefend Evaluation Summary");
        md.Should().Contain("100.0%");
        md.Should().Contain("False Positives (Benign):** 0 / 1");
        md.Should().Contain("✅ PASS");
        md.Should().Contain("RansomwareFast");
    }
}

using Xunit;

namespace ActDefend.IntegrationTests;

/// <summary>
/// Integration test placeholder for Phase 1.
/// 
/// Real integration tests (Phase 2+) will require:
/// - Administrator elevation
/// - Access to ETW kernel providers
/// - A controlled test workspace
///
/// Run integration tests separately from unit tests:
///   dotnet test tests/Detector.IntegrationTests --no-build
///
/// These must NOT be run in standard CI without admin rights.
/// </summary>
public sealed class PipelinePlaceholderTests
{
    [Fact(Skip = "Integration tests require elevation. Run manually as Administrator.")]
    public void EtwCollector_StartsAndStops_WithoutError()
    {
        // Phase 2: validate real ETW session start/stop lifecycle.
        Assert.True(true);
    }
}

namespace ActDefend.Core.Configuration;

public static class ConfigurationProfileHelper
{
    public static void ApplyProfile(ActDefendOptions options, ConfigurationProfile profile)
    {
        if (profile == ConfigurationProfile.Custom) return;

        switch (profile)
        {
            case ConfigurationProfile.Balanced:
                // Stage 1
                options.Stage1 = new Stage1Options
                {
                    SuspicionThreshold = 60.0,
                    Weights = new Stage1Weights
                    {
                        WriteRate = 10.0,
                        UniqueFilesWritten = 15.0,
                        RenameRate = 20.0,
                        DirectorySpread = 20.0,
                        WriteReadRatio = 10.0,
                        PreExistingModifyRate = 25.0
                    },
                    Thresholds = new Stage1Thresholds
                    {
                        WriteRatePerSec = 10.0,
                        UniqueFilesPerWindow = 30,
                        RenameRatePerSec = 5.0,
                        UniqueDirectoriesPerWindow = 10,
                        WriteReadRatioMax = 5.0,
                        PreExistingModifyRatePerSec = 5.0
                    }
                };
                // Stage 2
                options.Stage2 = new Stage2Options
                {
                    EntropyThreshold = 7.2,
                    MaxFilesToSample = 5,
                    ConfirmationMinFiles = 2,
                    CooldownSeconds = 10
                };
                // Windows
                options.Features = new FeaturesOptions
                {
                    PrimaryWindowSeconds = 5,
                    ContextWindowSeconds = 15,
                    EmitIntervalSeconds = 2,
                    InactivityExpirySeconds = 120
                };
                // Collector
                options.Collector = new CollectorOptions
                {
                    EventQueueCapacity = 4096
                };
                break;

            case ConfigurationProfile.Sensitive:
                options.Stage1.SuspicionThreshold = 45.0; // Lower threshold
                options.Stage1.Weights.RenameRate = 25.0;
                options.Stage1.Weights.PreExistingModifyRate = 30.0;
                // Normalize weights to 100
                options.Stage1.Weights.WriteRate = 10.0;
                options.Stage1.Weights.UniqueFilesWritten = 15.0;
                options.Stage1.Weights.DirectorySpread = 15.0;
                options.Stage1.Weights.WriteReadRatio = 5.0;

                options.Stage1.Thresholds.WriteRatePerSec = 5.0;
                options.Stage1.Thresholds.RenameRatePerSec = 3.0;

                options.Stage2.EntropyThreshold = 7.0;
                options.Stage2.MaxFilesToSample = 10;
                options.Stage2.ConfirmationMinFiles = 1;
                options.Stage2.CooldownSeconds = 5;

                options.Features.PrimaryWindowSeconds = 3;
                options.Features.ContextWindowSeconds = 10;
                break;

            case ConfigurationProfile.LowResource:
                options.Stage1.SuspicionThreshold = 70.0;
                options.Stage1.Thresholds.WriteRatePerSec = 15.0;
                options.Stage1.Thresholds.RenameRatePerSec = 10.0;

                options.Stage2.EntropyThreshold = 7.5;
                options.Stage2.MaxFilesToSample = 2;
                options.Stage2.ConfirmationMinFiles = 2;
                options.Stage2.CooldownSeconds = 30;

                options.Features.PrimaryWindowSeconds = 10;
                options.Features.ContextWindowSeconds = 30;
                options.Features.EmitIntervalSeconds = 5;
                options.Features.InactivityExpirySeconds = 60;

                options.Collector.EventQueueCapacity = 1024;
                break;

            case ConfigurationProfile.Conservative:
                options.Stage1.SuspicionThreshold = 75.0; // Higher threshold
                options.Stage1.Weights.PreExistingModifyRate = 40.0; // Demand pre-existing modification heavily
                options.Stage1.Weights.RenameRate = 25.0;
                options.Stage1.Weights.WriteRate = 5.0;
                options.Stage1.Weights.UniqueFilesWritten = 10.0;
                options.Stage1.Weights.DirectorySpread = 10.0;
                options.Stage1.Weights.WriteReadRatio = 10.0;
                
                options.Stage1.Thresholds.WriteReadRatioMax = 3.0;
                options.Stage1.Thresholds.PreExistingModifyRatePerSec = 10.0;

                options.Stage2.EntropyThreshold = 7.6;
                options.Stage2.MaxFilesToSample = 5;
                options.Stage2.ConfirmationMinFiles = 3;
                options.Stage2.CooldownSeconds = 15;

                options.Features.PrimaryWindowSeconds = 5;
                options.Features.ContextWindowSeconds = 20;
                break;
        }
    }
    /// <summary>
    /// Returns a fully-populated <see cref="ActDefendOptions"/> pre-configured for the given profile.
    /// This is the single source of truth used by both the dashboard settings UI and
    /// the Detector.Evaluation benchmark runner, ensuring evaluation profiles match
    /// exactly what users see in the application.
    /// </summary>
    public static ActDefendOptions GetProfileOptions(ConfigurationProfile profile)
    {
        // Start from the Balanced (default) base
        var options = new ActDefendOptions();
        // Apply Balanced first to populate ALL fields correctly
        ApplyProfile(options, ConfigurationProfile.Balanced);
        // Then overlay the requested profile
        if (profile != ConfigurationProfile.Balanced)
            ApplyProfile(options, profile);
        return options;
    }

    /// <summary>
    /// Returns options dictionaries suitable for in-memory configuration injection.
    /// Converts the typed options into the flat key=value format that
    /// <see cref="Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions.AddInMemoryCollection"/> accepts.
    /// </summary>
    public static Dictionary<string, string?> GetProfileConfigValues(ConfigurationProfile profile)
    {
        var o = GetProfileOptions(profile);
        return new Dictionary<string, string?>
        {
            ["ActDefend:Storage:DatabasePath"]                               = "PLACEHOLDER",
            ["ActDefend:Stage1:SuspicionThreshold"]                          = o.Stage1.SuspicionThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Weights:WriteRate"]                           = o.Stage1.Weights.WriteRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Weights:UniqueFilesWritten"]                  = o.Stage1.Weights.UniqueFilesWritten.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Weights:RenameRate"]                          = o.Stage1.Weights.RenameRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Weights:DirectorySpread"]                     = o.Stage1.Weights.DirectorySpread.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Weights:WriteReadRatio"]                      = o.Stage1.Weights.WriteReadRatio.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Weights:PreExistingModifyRate"]               = o.Stage1.Weights.PreExistingModifyRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Thresholds:WriteRatePerSec"]                  = o.Stage1.Thresholds.WriteRatePerSec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Thresholds:UniqueFilesPerWindow"]             = o.Stage1.Thresholds.UniqueFilesPerWindow.ToString(),
            ["ActDefend:Stage1:Thresholds:RenameRatePerSec"]                 = o.Stage1.Thresholds.RenameRatePerSec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Thresholds:UniqueDirectoriesPerWindow"]       = o.Stage1.Thresholds.UniqueDirectoriesPerWindow.ToString(),
            ["ActDefend:Stage1:Thresholds:WriteReadRatioMax"]                = o.Stage1.Thresholds.WriteReadRatioMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage1:Thresholds:PreExistingModifyRatePerSec"]      = o.Stage1.Thresholds.PreExistingModifyRatePerSec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage2:EntropyThreshold"]                            = o.Stage2.EntropyThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActDefend:Stage2:SampleBytesLimit"]                            = o.Stage2.SampleBytesLimit.ToString(),
            ["ActDefend:Stage2:MaxFilesToSample"]                            = o.Stage2.MaxFilesToSample.ToString(),
            ["ActDefend:Stage2:ConfirmationMinFiles"]                        = o.Stage2.ConfirmationMinFiles.ToString(),
            ["ActDefend:Stage2:CooldownSeconds"]                             = o.Stage2.CooldownSeconds.ToString(),
            ["ActDefend:Features:PrimaryWindowSeconds"]                      = o.Features.PrimaryWindowSeconds.ToString(),
            ["ActDefend:Features:ContextWindowSeconds"]                      = o.Features.ContextWindowSeconds.ToString(),
            ["ActDefend:Features:EmitIntervalSeconds"]                       = "1",  // Always use 1s emit for evaluation speed
            ["ActDefend:Features:InactivityExpirySeconds"]                   = o.Features.InactivityExpirySeconds.ToString(),
            ["ActDefend:Collector:EventQueueCapacity"]                       = o.Collector.EventQueueCapacity.ToString(),
            ["ActDefend:Collector:EventQueueTimeoutMs"]                      = o.Collector.EventQueueTimeoutMs.ToString(),
            ["ActDefend:TrustedProcesses:DefaultExclusions:0"]               = "System",
            ["ActDefend:TrustedProcesses:DefaultExclusions:1"]               = "smss.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:2"]               = "csrss.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:3"]               = "wininit.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:4"]               = "winlogon.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:5"]               = "services.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:6"]               = "lsass.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:7"]               = "svchost.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:8"]               = "MsMpEng.exe",
            ["ActDefend:TrustedProcesses:DefaultExclusions:9"]               = "SearchIndexer.exe",
        };
    }
}

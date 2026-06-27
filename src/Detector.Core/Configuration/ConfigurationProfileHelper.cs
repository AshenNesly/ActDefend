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
}

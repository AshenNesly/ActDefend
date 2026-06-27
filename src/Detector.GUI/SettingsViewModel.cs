using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;

namespace ActDefend.GUI;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IConfigurationManager _configManager;
    private ActDefendOptions _draftOptions;

    public SettingsViewModel(IConfigurationManager configManager)
    {
        _configManager = configManager;
        // Deep copy not strictly necessary if we manually map fields, but let's just 
        // initialize with what's current so the UI starts populated.
        _draftOptions = CopyOptions(_configManager.CurrentOptions);
        
        ApplyProfileCommand = new RelayCommand(ExecuteApplyProfile);
        ResetToDefaultsCommand = new RelayCommand(ExecuteResetToDefaults);
        SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);
        
        LoadFromDraft();
    }

    private ActDefendOptions CopyOptions(ActDefendOptions source)
    {
        // Simple manual copy of the relevant tuned options
        var clone = new ActDefendOptions
        {
            Stage1 = new Stage1Options
            {
                SuspicionThreshold = source.Stage1.SuspicionThreshold,
                Weights = new Stage1Weights
                {
                    WriteRate = source.Stage1.Weights.WriteRate,
                    UniqueFilesWritten = source.Stage1.Weights.UniqueFilesWritten,
                    RenameRate = source.Stage1.Weights.RenameRate,
                    DirectorySpread = source.Stage1.Weights.DirectorySpread,
                    WriteReadRatio = source.Stage1.Weights.WriteReadRatio,
                    PreExistingModifyRate = source.Stage1.Weights.PreExistingModifyRate
                },
                Thresholds = new Stage1Thresholds
                {
                    WriteRatePerSec = source.Stage1.Thresholds.WriteRatePerSec,
                    UniqueFilesPerWindow = source.Stage1.Thresholds.UniqueFilesPerWindow,
                    RenameRatePerSec = source.Stage1.Thresholds.RenameRatePerSec,
                    UniqueDirectoriesPerWindow = source.Stage1.Thresholds.UniqueDirectoriesPerWindow,
                    WriteReadRatioMax = source.Stage1.Thresholds.WriteReadRatioMax,
                    PreExistingModifyRatePerSec = source.Stage1.Thresholds.PreExistingModifyRatePerSec
                }
            },
            Stage2 = new Stage2Options
            {
                EntropyThreshold = source.Stage2.EntropyThreshold,
                SampleBytesLimit = source.Stage2.SampleBytesLimit,
                MaxFilesToSample = source.Stage2.MaxFilesToSample,
                ConfirmationMinFiles = source.Stage2.ConfirmationMinFiles,
                CooldownSeconds = source.Stage2.CooldownSeconds
            },
            Features = new FeaturesOptions
            {
                PrimaryWindowSeconds = source.Features.PrimaryWindowSeconds,
                ContextWindowSeconds = source.Features.ContextWindowSeconds,
                EmitIntervalSeconds = source.Features.EmitIntervalSeconds,
                InactivityExpirySeconds = source.Features.InactivityExpirySeconds
            },
            Collector = new CollectorOptions
            {
                EventQueueCapacity = source.Collector.EventQueueCapacity,
                EventQueueTimeoutMs = source.Collector.EventQueueTimeoutMs
            }
        };
        return clone;
    }

    public ICommand ApplyProfileCommand { get; }
    public ICommand ResetToDefaultsCommand { get; }
    public ICommand SaveSettingsCommand { get; }

    // ── Validation and State ──

    private string _validationErrorMessage = string.Empty;
    public string ValidationErrorMessage
    {
        get => _validationErrorMessage;
        set { _validationErrorMessage = value; OnPropertyChanged(); }
    }

    private bool _isRestartRequired = false;
    public bool IsRestartRequired
    {
        get => _isRestartRequired;
        set { _isRestartRequired = value; OnPropertyChanged(); }
    }

    private ConfigurationProfile _selectedProfile = ConfigurationProfile.Custom;
    public ConfigurationProfile SelectedProfile
    {
        get => _selectedProfile;
        set 
        {
            if (_selectedProfile != value)
            {
                _selectedProfile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentProfileDescription));
            }
        }
    }

    public string CurrentProfileDescription => SelectedProfile switch
    {
        ConfigurationProfile.Balanced => "Default recommended profile. Good balance between detection accuracy and false positives.",
        ConfigurationProfile.Sensitive => "Detects earlier. May increase false positives.",
        ConfigurationProfile.LowResource => "Reduces CPU/memory pressure. May detect slightly slower.",
        ConfigurationProfile.Conservative => "Reduces false positives. May miss very slow or weak ransomware-like behaviour.",
        _ => "Custom user configuration."
    };

    // ── Properties (Stage 1) ──

    public double Stage1SuspicionThreshold
    {
        get => _draftOptions.Stage1.SuspicionThreshold;
        set { _draftOptions.Stage1.SuspicionThreshold = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1WeightWriteRate
    {
        get => _draftOptions.Stage1.Weights.WriteRate;
        set { _draftOptions.Stage1.Weights.WriteRate = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1WeightUniqueFilesWritten
    {
        get => _draftOptions.Stage1.Weights.UniqueFilesWritten;
        set { _draftOptions.Stage1.Weights.UniqueFilesWritten = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1WeightRenameRate
    {
        get => _draftOptions.Stage1.Weights.RenameRate;
        set { _draftOptions.Stage1.Weights.RenameRate = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1WeightDirectorySpread
    {
        get => _draftOptions.Stage1.Weights.DirectorySpread;
        set { _draftOptions.Stage1.Weights.DirectorySpread = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1WeightWriteReadRatio
    {
        get => _draftOptions.Stage1.Weights.WriteReadRatio;
        set { _draftOptions.Stage1.Weights.WriteReadRatio = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1WeightPreExistingModifyRate
    {
        get => _draftOptions.Stage1.Weights.PreExistingModifyRate;
        set { _draftOptions.Stage1.Weights.PreExistingModifyRate = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }

    // Thresholds
    public double Stage1ThresholdWriteRatePerSec
    {
        get => _draftOptions.Stage1.Thresholds.WriteRatePerSec;
        set { _draftOptions.Stage1.Thresholds.WriteRatePerSec = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int Stage1ThresholdUniqueFilesPerWindow
    {
        get => _draftOptions.Stage1.Thresholds.UniqueFilesPerWindow;
        set { _draftOptions.Stage1.Thresholds.UniqueFilesPerWindow = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1ThresholdRenameRatePerSec
    {
        get => _draftOptions.Stage1.Thresholds.RenameRatePerSec;
        set { _draftOptions.Stage1.Thresholds.RenameRatePerSec = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int Stage1ThresholdUniqueDirectoriesPerWindow
    {
        get => _draftOptions.Stage1.Thresholds.UniqueDirectoriesPerWindow;
        set { _draftOptions.Stage1.Thresholds.UniqueDirectoriesPerWindow = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1ThresholdWriteReadRatioMax
    {
        get => _draftOptions.Stage1.Thresholds.WriteReadRatioMax;
        set { _draftOptions.Stage1.Thresholds.WriteReadRatioMax = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public double Stage1ThresholdPreExistingModifyRatePerSec
    {
        get => _draftOptions.Stage1.Thresholds.PreExistingModifyRatePerSec;
        set { _draftOptions.Stage1.Thresholds.PreExistingModifyRatePerSec = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }

    // ── Properties (Stage 2) ──
    public double Stage2EntropyThreshold
    {
        get => _draftOptions.Stage2.EntropyThreshold;
        set { _draftOptions.Stage2.EntropyThreshold = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int Stage2MaxFilesToSample
    {
        get => _draftOptions.Stage2.MaxFilesToSample;
        set { _draftOptions.Stage2.MaxFilesToSample = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int Stage2ConfirmationMinFiles
    {
        get => _draftOptions.Stage2.ConfirmationMinFiles;
        set { _draftOptions.Stage2.ConfirmationMinFiles = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int Stage2CooldownSeconds
    {
        get => _draftOptions.Stage2.CooldownSeconds;
        set { _draftOptions.Stage2.CooldownSeconds = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }

    // ── Properties (Features) ──
    public int FeaturesPrimaryWindowSeconds
    {
        get => _draftOptions.Features.PrimaryWindowSeconds;
        set { _draftOptions.Features.PrimaryWindowSeconds = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int FeaturesContextWindowSeconds
    {
        get => _draftOptions.Features.ContextWindowSeconds;
        set { _draftOptions.Features.ContextWindowSeconds = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int FeaturesEmitIntervalSeconds
    {
        get => _draftOptions.Features.EmitIntervalSeconds;
        set { _draftOptions.Features.EmitIntervalSeconds = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }
    public int FeaturesInactivityExpirySeconds
    {
        get => _draftOptions.Features.InactivityExpirySeconds;
        set { _draftOptions.Features.InactivityExpirySeconds = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }

    // ── Properties (Collector) ──
    public int CollectorEventQueueCapacity
    {
        get => _draftOptions.Collector.EventQueueCapacity;
        set { _draftOptions.Collector.EventQueueCapacity = value; SelectedProfile = ConfigurationProfile.Custom; OnPropertyChanged(); Validate(); }
    }


    private void LoadFromDraft()
    {
        // Fire PropertyChanged for everything
        OnPropertyChanged(string.Empty);
        Validate();
    }

    private void ExecuteApplyProfile(object? parameter)
    {
        if (parameter is string profileStr && Enum.TryParse<ConfigurationProfile>(profileStr, out var profile))
        {
            ConfigurationProfileHelper.ApplyProfile(_draftOptions, profile);
            SelectedProfile = profile;
            LoadFromDraft();
        }
    }

    private void ExecuteResetToDefaults(object? parameter)
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to the default Balanced profile?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ConfigurationProfileHelper.ApplyProfile(_draftOptions, ConfigurationProfile.Balanced);
            SelectedProfile = ConfigurationProfile.Balanced;
            LoadFromDraft();
        }
    }

    private async void ExecuteSaveSettings(object? parameter)
    {
        if (!string.IsNullOrEmpty(ValidationErrorMessage))
        {
            MessageBox.Show("Please fix validation errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _configManager.SaveAsync(_draftOptions);
            IsRestartRequired = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Validate()
    {
        string error = string.Empty;

        if (Stage1SuspicionThreshold < 40 || Stage1SuspicionThreshold > 90) error = "SuspicionThreshold must be between 40 and 90.";
        else if (Stage2EntropyThreshold < 6.5 || Stage2EntropyThreshold > 8.0) error = "EntropyThreshold must be between 6.5 and 8.0.";
        else if (Stage2ConfirmationMinFiles < 1 || Stage2ConfirmationMinFiles > 10) error = "ConfirmationMinFiles must be between 1 and 10.";
        else if (Stage2MaxFilesToSample < 1 || Stage2MaxFilesToSample > 50) error = "MaxFilesToSample must be between 1 and 50.";
        else if (FeaturesPrimaryWindowSeconds < 2 || FeaturesPrimaryWindowSeconds > 15) error = "PrimaryWindowSeconds must be between 2 and 15.";
        else if (FeaturesContextWindowSeconds < 5 || FeaturesContextWindowSeconds > 60) error = "ContextWindowSeconds must be between 5 and 60.";
        else if (FeaturesContextWindowSeconds < FeaturesPrimaryWindowSeconds) error = "ContextWindowSeconds must be >= PrimaryWindowSeconds.";
        else if (FeaturesEmitIntervalSeconds < 1 || FeaturesEmitIntervalSeconds > 10) error = "EmitIntervalSeconds must be between 1 and 10.";
        else if (FeaturesInactivityExpirySeconds < 30 || FeaturesInactivityExpirySeconds > 600) error = "InactivityExpirySeconds must be between 30 and 600.";
        else if (CollectorEventQueueCapacity < 1024 || CollectorEventQueueCapacity > 100000) error = "EventQueueCapacity must be between 1024 and 100000.";

        // Weights
        else if (Stage1WeightWriteRate < 0 || Stage1WeightWriteRate > 50) error = "WriteRate weight must be 0-50.";
        else if (Stage1WeightUniqueFilesWritten < 0 || Stage1WeightUniqueFilesWritten > 50) error = "UniqueFilesWritten weight must be 0-50.";
        else if (Stage1WeightRenameRate < 0 || Stage1WeightRenameRate > 50) error = "RenameRate weight must be 0-50.";
        else if (Stage1WeightDirectorySpread < 0 || Stage1WeightDirectorySpread > 50) error = "DirectorySpread weight must be 0-50.";
        else if (Stage1WeightWriteReadRatio < 0 || Stage1WeightWriteReadRatio > 50) error = "WriteReadRatio weight must be 0-50.";
        else if (Stage1WeightPreExistingModifyRate < 0 || Stage1WeightPreExistingModifyRate > 50) error = "PreExistingModifyRate weight must be 0-50.";
        else
        {
            double totalWeight = Stage1WeightWriteRate + Stage1WeightUniqueFilesWritten + Stage1WeightRenameRate + 
                                 Stage1WeightDirectorySpread + Stage1WeightWriteReadRatio + Stage1WeightPreExistingModifyRate;
            if (totalWeight > 100.0) error = $"Total Stage 1 weights cannot exceed 100 (currently {totalWeight:F1}).";
        }

        ValidationErrorMessage = error;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

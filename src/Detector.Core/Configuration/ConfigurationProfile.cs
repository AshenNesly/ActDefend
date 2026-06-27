namespace ActDefend.Core.Configuration;

/// <summary>
/// Preset configuration profiles that balance detection sensitivity and system resource usage.
/// </summary>
public enum ConfigurationProfile
{
    /// <summary>
    /// Default recommended profile. Good balance between detection accuracy and false positives.
    /// </summary>
    Balanced,

    /// <summary>
    /// Detects earlier. May increase false positives.
    /// </summary>
    Sensitive,

    /// <summary>
    /// Reduces CPU/memory pressure. May detect slightly slower.
    /// </summary>
    LowResource,

    /// <summary>
    /// Reduces false positives. May miss very slow or weak ransomware-like behaviour.
    /// </summary>
    Conservative,

    /// <summary>
    /// Represents a user-modified configuration that doesn't match any preset.
    /// </summary>
    Custom
}

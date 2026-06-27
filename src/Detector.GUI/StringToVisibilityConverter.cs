using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

using System.Windows.Markup;

namespace ActDefend.GUI;

/// <summary>
/// Converts a string to Visibility.
/// Visible if the string is non-null and non-empty; Collapsed otherwise.
/// Used for the Settings validation error label.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class StringToVisibilityConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

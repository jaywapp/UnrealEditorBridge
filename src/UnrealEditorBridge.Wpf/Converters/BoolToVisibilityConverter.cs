using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UnrealEditorBridge.Wpf.Converters
{

    /// <summary>
    /// bool 값을 <see cref="Visibility"/>로 변환한다.
    /// true → Visible, false → Collapsed.
    /// ConverterParameter에 "Invert"를 지정하면 반전된다.
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        #region Functions

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = value is true;

            if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible;

            return false;
        }

        #endregion
    }
}

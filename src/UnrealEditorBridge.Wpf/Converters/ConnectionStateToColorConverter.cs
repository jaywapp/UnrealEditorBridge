using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UnrealEditorBridge.Adapter;

namespace UnrealEditorBridge.Wpf.Converters
{

    /// <summary>
    /// <see cref="ConnectionState"/>를 색상 브러시로 변환한다.
    /// </summary>
    public sealed class ConnectionStateToColorConverter : IValueConverter
    {
        #region Functions

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ConnectionState state)
                return Brushes.Gray;

            return state switch
            {
                ConnectionState.Disconnected => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                ConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                ConnectionState.Lost => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                ConnectionState.VersionMismatch => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                ConnectionState.Error => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                _ => Brushes.Gray
            };
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}

using System.Globalization;
using System.Windows.Data;
using UnrealEditorBridge.Adapter;

namespace UnrealEditorBridge.Wpf.Converters
{

    /// <summary>
    /// <see cref="ConnectionState"/>를 bool로 변환한다.
    /// parameter에 지정된 상태와 일치하면 true를 반환한다.
    /// </summary>
    public sealed class ConnectionStateToBoolConverter : IValueConverter
    {
        #region Functions

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ConnectionState state)
                return false;

            if (parameter is string stateString &&
                Enum.TryParse<ConnectionState>(stateString, out var targetState))
            {
                return state == targetState;
            }

            return state == ConnectionState.Connected;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}

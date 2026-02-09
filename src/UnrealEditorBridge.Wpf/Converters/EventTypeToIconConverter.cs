using System.Globalization;
using System.Windows.Data;
using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Wpf.Converters
{

    /// <summary>
    /// <see cref="AssetEventType"/>을 아이콘 텍스트로 변환한다.
    /// </summary>
    public sealed class EventTypeToIconConverter : IValueConverter
    {
        #region Functions

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not AssetEventType eventType)
                return "?";

            return eventType switch
            {
                AssetEventType.AssetCreated => "+",
                AssetEventType.AssetDeleted => "X",
                AssetEventType.AssetRenamed => "R",
                AssetEventType.AssetSaved => "S",
                AssetEventType.AssetMoved => "M",
                AssetEventType.AssetTagsChanged => "T",
                AssetEventType.AssetLoaded => "L",
                AssetEventType.AssetDependencyChanged => "D",
                AssetEventType.SnapshotUpdated => "!",
                AssetEventType.EditorShutdown => "P",
                _ => "?"
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

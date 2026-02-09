using ReactiveUI;
using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Wpf.ViewModels
{

    /// <summary>
    /// 에셋 목록의 개별 항목 ViewModel.
    /// </summary>
    public class AssetItemViewModel : ReactiveObject
    {
        #region Internal Fields

        private string _assetName;
        private string _className;
        private string _objectPath;

        #endregion

        #region Properties

        /// <summary>원본 에셋 정보.</summary>
        public AssetInfo AssetInfo { get; }

        /// <summary>에셋 이름.</summary>
        public string AssetName
        {
            get => _assetName;
            set => this.RaiseAndSetIfChanged(ref _assetName, value);
        }

        /// <summary>에셋 클래스명.</summary>
        public string ClassName
        {
            get => _className;
            set => this.RaiseAndSetIfChanged(ref _className, value);
        }

        /// <summary>에셋 오브젝트 경로.</summary>
        public string ObjectPath
        {
            get => _objectPath;
            set => this.RaiseAndSetIfChanged(ref _objectPath, value);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="AssetItemViewModel"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="assetInfo">에셋 정보.</param>
        public AssetItemViewModel(AssetInfo assetInfo)
        {
            AssetInfo = assetInfo;
            _assetName = assetInfo.AssetName;
            _className = assetInfo.ClassName;
            _objectPath = assetInfo.ObjectPath;
        }

        #endregion
    }
}

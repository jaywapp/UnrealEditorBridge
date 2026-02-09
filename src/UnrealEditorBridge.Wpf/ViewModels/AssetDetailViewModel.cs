using System.Collections.ObjectModel;
using ReactiveUI;
using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Wpf.ViewModels
{

    /// <summary>
    /// 선택된 에셋의 상세 정보를 표시하는 ViewModel.
    /// </summary>
    public class AssetDetailViewModel : ReactiveObject
    {
        #region Internal Fields

        private string? _objectPath;
        private string? _packagePath;
        private string? _assetName;
        private string? _className;
        private bool _hasSelection;

        #endregion

        #region Properties

        /// <summary>에셋 오브젝트 경로.</summary>
        public string? ObjectPath
        {
            get => _objectPath;
            set => this.RaiseAndSetIfChanged(ref _objectPath, value);
        }

        /// <summary>에셋 패키지 경로.</summary>
        public string? PackagePath
        {
            get => _packagePath;
            set => this.RaiseAndSetIfChanged(ref _packagePath, value);
        }

        /// <summary>에셋 이름.</summary>
        public string? AssetName
        {
            get => _assetName;
            set => this.RaiseAndSetIfChanged(ref _assetName, value);
        }

        /// <summary>에셋 클래스명.</summary>
        public string? ClassName
        {
            get => _className;
            set => this.RaiseAndSetIfChanged(ref _className, value);
        }

        /// <summary>에셋이 선택되어 있는지 여부.</summary>
        public bool HasSelection
        {
            get => _hasSelection;
            set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
        }

        /// <summary>에셋 태그 목록.</summary>
        public ObservableCollection<KeyValuePair<string, string>> Tags { get; } = new();

        /// <summary>하드 의존성 경로 목록.</summary>
        public ObservableCollection<string> HardDependencies { get; } = new();

        /// <summary>소프트 의존성 경로 목록.</summary>
        public ObservableCollection<string> SoftDependencies { get; } = new();

        #endregion

        #region Functions

        /// <summary>
        /// 지정된 에셋의 상세 정보를 표시한다.
        /// </summary>
        /// <param name="asset">표시할 에셋 정보. null이면 선택 해제.</param>
        public void ShowAsset(AssetInfo? asset)
        {
            if (asset == null)
            {
                HasSelection = false;
                ObjectPath = null;
                PackagePath = null;
                AssetName = null;
                ClassName = null;
                Tags.Clear();
                HardDependencies.Clear();
                SoftDependencies.Clear();
                return;
            }

            HasSelection = true;
            ObjectPath = asset.ObjectPath;
            PackagePath = asset.PackagePath;
            AssetName = asset.AssetName;
            ClassName = asset.ClassName;

            Tags.Clear();
            foreach (var tag in asset.Tags)
            {
                Tags.Add(tag);
            }

            HardDependencies.Clear();
            foreach (var dep in asset.Dependencies.Hard)
            {
                HardDependencies.Add(dep);
            }

            SoftDependencies.Clear();
            foreach (var dep in asset.Dependencies.Soft)
            {
                SoftDependencies.Add(dep);
            }
        }

        #endregion
    }
}

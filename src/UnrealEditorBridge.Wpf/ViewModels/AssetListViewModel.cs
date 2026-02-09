using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Prism.Commands;
using ReactiveUI;
using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Adapter.Models;
using UnrealEditorBridge.Protocol;
using UnrealEditorBridge.Wpf.Services;

namespace UnrealEditorBridge.Wpf.ViewModels
{

    /// <summary>
    /// 에셋 목록 표시 및 필터링 ViewModel.
    /// </summary>
    public class AssetListViewModel : ReactiveObject
    {
        #region Internal Fields

        private readonly IBridgeService _bridgeService;
        private List<AssetInfo> _allAssets = new();

        private string? _searchText;
        private string? _classFilter;
        private string? _pathFilter;
        private AssetItemViewModel? _selectedAsset;
        private int _totalCount;
        private int _filteredCount;

        #endregion

        #region Properties

        /// <summary>필터링된 에셋 목록.</summary>
        public ObservableCollection<AssetItemViewModel> FilteredAssets { get; } = new();

        /// <summary>검색 텍스트 (에셋 이름 / 경로).</summary>
        public string? SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }

        /// <summary>클래스 필터.</summary>
        public string? ClassFilter
        {
            get => _classFilter;
            set => this.RaiseAndSetIfChanged(ref _classFilter, value);
        }

        /// <summary>경로 필터.</summary>
        public string? PathFilter
        {
            get => _pathFilter;
            set => this.RaiseAndSetIfChanged(ref _pathFilter, value);
        }

        /// <summary>현재 선택된 에셋.</summary>
        public AssetItemViewModel? SelectedAsset
        {
            get => _selectedAsset;
            set => this.RaiseAndSetIfChanged(ref _selectedAsset, value);
        }

        /// <summary>전체 에셋 개수.</summary>
        public int TotalCount
        {
            get => _totalCount;
            set => this.RaiseAndSetIfChanged(ref _totalCount, value);
        }

        /// <summary>필터링된 에셋 개수.</summary>
        public int FilteredCount
        {
            get => _filteredCount;
            set => this.RaiseAndSetIfChanged(ref _filteredCount, value);
        }

        /// <summary>사용 가능한 클래스 목록.</summary>
        public ObservableCollection<string> AvailableClasses { get; } = new();

        #endregion

        #region Commands

        /// <summary>필터를 초기화하는 커맨드.</summary>
        public DelegateCommand ClearFiltersCommand { get; }

        /// <summary>스냅샷을 새로고침하는 커맨드.</summary>
        public DelegateCommand RefreshCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="AssetListViewModel"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="bridgeService">Bridge 서비스.</param>
        public AssetListViewModel(IBridgeService bridgeService)
        {
            _bridgeService = bridgeService;

            ClearFiltersCommand = new DelegateCommand(ExecuteClearFilters);
            RefreshCommand = new DelegateCommand(async () => await ExecuteRefreshAsync());

            // 필터 변경 시 300ms 디바운싱 후 목록 재구성
            this.WhenAnyValue(
                    x => x.SearchText,
                    x => x.ClassFilter,
                    x => x.PathFilter)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => RebuildFilteredList());

            // Snapshot / Event 구독
            _bridgeService.SnapshotReceived += OnSnapshotReceived;
            _bridgeService.EventReceived += OnEventReceived;
        }

        #endregion

        #region Event Handlers

        private void OnSnapshotReceived(object? sender, SnapshotReceivedEventArgs e)
        {
            _allAssets = e.Snapshot.Assets.ToList();
            UpdateAvailableClasses();
            RebuildFilteredList();
        }

        private void OnEventReceived(object? sender, AssetEventReceivedEventArgs e)
        {
            switch (e.Event.EventType)
            {
                case AssetEventType.AssetCreated:
                    AddAssetFromEvent(e.Event);
                    break;
                case AssetEventType.AssetDeleted:
                    RemoveAssetByPath(e.Event.ObjectPath);
                    break;
                case AssetEventType.AssetRenamed:
                    if (e.Event.OldObjectPath != null)
                        RemoveAssetByPath(e.Event.OldObjectPath);
                    AddAssetFromEvent(e.Event);
                    break;
            }
        }

        #endregion

        #region Functions

        private void RebuildFilteredList()
        {
            var filtered = ApplyFilters(_allAssets);

            FilteredAssets.Clear();
            foreach (var asset in filtered)
            {
                FilteredAssets.Add(new AssetItemViewModel(asset));
            }

            TotalCount = _allAssets.Count;
            FilteredCount = filtered.Count;
        }

        private List<AssetInfo> ApplyFilters(List<AssetInfo> source)
        {
            IEnumerable<AssetInfo> query = source;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLowerInvariant();
                query = query.Where(a =>
                    a.AssetName.ToLowerInvariant().Contains(term) ||
                    a.ObjectPath.ToLowerInvariant().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(ClassFilter))
            {
                query = query.Where(a => a.ClassName == ClassFilter);
            }

            if (!string.IsNullOrWhiteSpace(PathFilter))
            {
                query = query.Where(a =>
                    a.PackagePath.StartsWith(PathFilter, StringComparison.OrdinalIgnoreCase));
            }

            return query.ToList();
        }

        private void UpdateAvailableClasses()
        {
            AvailableClasses.Clear();
            var classes = _allAssets
                .Select(a => a.ClassName)
                .Distinct()
                .OrderBy(c => c);
            foreach (var cls in classes)
            {
                AvailableClasses.Add(cls);
            }
        }

        private void AddAssetFromEvent(AssetEvent evt)
        {
            var info = new AssetInfo
            {
                ObjectPath = evt.ObjectPath,
                AssetName = evt.AssetName ?? string.Empty,
                ClassName = evt.ClassName ?? string.Empty,
            };
            _allAssets.Add(info);
            TotalCount = _allAssets.Count;

            if (MatchesFilter(info))
            {
                FilteredAssets.Add(new AssetItemViewModel(info));
                FilteredCount = FilteredAssets.Count;
            }
        }

        private void RemoveAssetByPath(string objectPath)
        {
            _allAssets.RemoveAll(a => a.ObjectPath == objectPath);
            TotalCount = _allAssets.Count;

            var toRemove = FilteredAssets.FirstOrDefault(vm => vm.ObjectPath == objectPath);
            if (toRemove != null)
            {
                FilteredAssets.Remove(toRemove);
                FilteredCount = FilteredAssets.Count;
            }
        }

        private bool MatchesFilter(AssetInfo asset)
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLowerInvariant();
                if (!asset.AssetName.ToLowerInvariant().Contains(term) &&
                    !asset.ObjectPath.ToLowerInvariant().Contains(term))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(ClassFilter) && asset.ClassName != ClassFilter)
                return false;

            if (!string.IsNullOrWhiteSpace(PathFilter) &&
                !asset.PackagePath.StartsWith(PathFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void ExecuteClearFilters()
        {
            SearchText = string.Empty;
            ClassFilter = null;
            PathFilter = null;
        }

        private async Task ExecuteRefreshAsync()
        {
            var snapshot = await _bridgeService.RefreshSnapshotAsync();
            _allAssets = snapshot.Assets.ToList();
            UpdateAvailableClasses();
            RebuildFilteredList();
        }

        #endregion
    }
}

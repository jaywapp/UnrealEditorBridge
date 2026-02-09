using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Prism.Commands;
using ReactiveUI;
using UnrealEditorBridge.Adapter.Events;
using UnrealEditorBridge.Adapter.Models;
using UnrealEditorBridge.Protocol;
using UnrealEditorBridge.Wpf.Models;
using UnrealEditorBridge.Wpf.Services;

namespace UnrealEditorBridge.Wpf.ViewModels
{

    /// <summary>
    /// 에셋 이벤트 스트림의 실시간 로그 ViewModel.
    /// </summary>
    public class EventLogViewModel : ReactiveObject
    {
        #region Const Fields

        private const int MaxLogItems = 10000;

        #endregion

        #region Internal Fields

        private readonly IBridgeService _bridgeService;

        private bool _showCreated = true;
        private bool _showDeleted = true;
        private bool _showRenamed = true;
        private bool _showSaved = true;
        private bool _showOther = true;
        private bool _autoScroll = true;
        private bool _hasOverflowWarning;
        private string? _overflowMessage;

        #endregion

        #region Properties

        /// <summary>이벤트 로그 항목 목록.</summary>
        public ObservableCollection<EventLogItem> Events { get; } = new();

        /// <summary>생성 이벤트 표시 여부.</summary>
        public bool ShowCreated
        {
            get => _showCreated;
            set => this.RaiseAndSetIfChanged(ref _showCreated, value);
        }

        /// <summary>삭제 이벤트 표시 여부.</summary>
        public bool ShowDeleted
        {
            get => _showDeleted;
            set => this.RaiseAndSetIfChanged(ref _showDeleted, value);
        }

        /// <summary>이름 변경 이벤트 표시 여부.</summary>
        public bool ShowRenamed
        {
            get => _showRenamed;
            set => this.RaiseAndSetIfChanged(ref _showRenamed, value);
        }

        /// <summary>저장 이벤트 표시 여부.</summary>
        public bool ShowSaved
        {
            get => _showSaved;
            set => this.RaiseAndSetIfChanged(ref _showSaved, value);
        }

        /// <summary>기타 이벤트 표시 여부.</summary>
        public bool ShowOther
        {
            get => _showOther;
            set => this.RaiseAndSetIfChanged(ref _showOther, value);
        }

        /// <summary>자동 스크롤 활성화 여부.</summary>
        public bool AutoScroll
        {
            get => _autoScroll;
            set => this.RaiseAndSetIfChanged(ref _autoScroll, value);
        }

        /// <summary>오버플로 경고 표시 여부.</summary>
        public bool HasOverflowWarning
        {
            get => _hasOverflowWarning;
            set => this.RaiseAndSetIfChanged(ref _hasOverflowWarning, value);
        }

        /// <summary>오버플로 경고 메시지.</summary>
        public string? OverflowMessage
        {
            get => _overflowMessage;
            set => this.RaiseAndSetIfChanged(ref _overflowMessage, value);
        }

        #endregion

        #region Commands

        /// <summary>로그를 지우는 커맨드.</summary>
        public DelegateCommand ClearLogCommand { get; }

        /// <summary>오버플로 경고를 닫는 커맨드.</summary>
        public DelegateCommand DismissOverflowWarningCommand { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// <see cref="EventLogViewModel"/>의 새 인스턴스를 초기화한다.
        /// </summary>
        /// <param name="bridgeService">Bridge 서비스.</param>
        public EventLogViewModel(IBridgeService bridgeService)
        {
            _bridgeService = bridgeService;

            ClearLogCommand = new DelegateCommand(() => Events.Clear());
            DismissOverflowWarningCommand = new DelegateCommand(
                () => HasOverflowWarning = false);

            _bridgeService.EventReceived += OnEventReceived;
            _bridgeService.EventOverflow += OnEventOverflow;
        }

        #endregion

        #region Event Handlers

        private void OnEventReceived(object? sender, AssetEventReceivedEventArgs e)
        {
            var item = new EventLogItem
            {
                SequenceNumber = e.Event.SequenceNumber,
                Timestamp = e.Event.Timestamp,
                EventType = e.Event.EventType,
                ObjectPath = e.Event.ObjectPath,
                Description = FormatEventDescription(e.Event)
            };

            if (!ShouldShow(item.EventType))
                return;

            Events.Insert(0, item);

            // 메모리 보호: 최대 항목 수 초과 시 오래된 항목 제거
            while (Events.Count > MaxLogItems)
            {
                Events.RemoveAt(Events.Count - 1);
            }
        }

        private void OnEventOverflow(object? sender, EventOverflowEventArgs e)
        {
            HasOverflowWarning = true;
            OverflowMessage = $"이벤트 {e.MissedCount}건 누락 - Snapshot 동기화 진행 중";
        }

        #endregion

        #region Functions

        private bool ShouldShow(AssetEventType type) => type switch
        {
            AssetEventType.AssetCreated => ShowCreated,
            AssetEventType.AssetDeleted => ShowDeleted,
            AssetEventType.AssetRenamed => ShowRenamed,
            AssetEventType.AssetSaved => ShowSaved,
            _ => ShowOther
        };

        private static string FormatEventDescription(AssetEvent evt) => evt.EventType switch
        {
            AssetEventType.AssetCreated => $"[생성] {evt.AssetName}",
            AssetEventType.AssetDeleted => $"[삭제] {evt.AssetName}",
            AssetEventType.AssetRenamed => $"[이름변경] {evt.OldAssetName} → {evt.AssetName}",
            AssetEventType.AssetSaved => $"[저장] {evt.AssetName}",
            AssetEventType.AssetMoved => $"[이동] {evt.AssetName}",
            AssetEventType.AssetTagsChanged => $"[태그변경] {evt.AssetName}",
            _ => $"[{evt.EventType}] {evt.ObjectPath}"
        };

        #endregion
    }
}

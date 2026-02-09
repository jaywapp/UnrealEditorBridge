namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// UnrealEditorBridge 프로토콜의 전역 상수를 정의한다.
    /// C++ Editor Plugin과 .NET Adapter 양측에서 동일한 값을 사용해야 한다.
    /// </summary>
    public static class ProtocolConstants
    {
        #region Const Fields

        /// <summary>MMF Header 매직 넘버. ASCII "UEB!" = 0x55454221.</summary>
        public const uint Magic = 0x55454221;

        /// <summary>Discovery MMF 매직 넘버. ASCII "UEBD" = 0x55454244.</summary>
        public const uint DiscoveryMagic = 0x55454244;

        /// <summary>현재 프로토콜 버전. Major * 1000 + Minor. v1.0 = 1000.</summary>
        public const uint ProtocolVersion = 1000;

        /// <summary>프로토콜 Major 버전.</summary>
        public const uint ProtocolMajor = 1;

        /// <summary>프로토콜 Minor 버전.</summary>
        public const uint ProtocolMinor = 0;

        /// <summary>Header 영역 크기 (바이트).</summary>
        public const int HeaderSize = 256;

        /// <summary>MMF 기본 전체 크기 (16 MB).</summary>
        public const int DefaultTotalMmfSize = 16 * 1024 * 1024;

        /// <summary>Snapshot 영역 기본 용량 (4 MB).</summary>
        public const int DefaultSnapshotCapacity = 4 * 1024 * 1024;

        /// <summary>Snapshot 영역 기본 시작 오프셋.</summary>
        public const int DefaultSnapshotOffset = HeaderSize; // 0x0100

        /// <summary>Event Ring Buffer 기본 시작 오프셋.</summary>
        public const int DefaultEventRingOffset = DefaultSnapshotOffset + DefaultSnapshotCapacity;

        /// <summary>개별 이벤트 슬롯 기본 크기 (바이트).</summary>
        public const int DefaultEventSlotSize = 2048;

        /// <summary>Ring Buffer 슬롯 기본 총 개수.</summary>
        public const int DefaultEventSlotCount = 6144;

        /// <summary>이벤트 슬롯 내부 고정 헤더 크기 (바이트).</summary>
        public const int EventRecordHeaderSize = 24;

        /// <summary>Discovery MMF 이름.</summary>
        public const string DiscoveryMmfName = "UEB_Discovery";

        /// <summary>Discovery MMF 크기 (64 KB).</summary>
        public const int DiscoveryMmfSize = 64 * 1024;

        /// <summary>Discovery Header 크기 (바이트).</summary>
        public const int DiscoveryHeaderSize = 64;

        /// <summary>Discovery 개별 엔트리 크기 (바이트).</summary>
        public const int DiscoveryEntrySize = 512;

        /// <summary>Discovery 최대 엔트리 수.</summary>
        public const int DiscoveryMaxEntries = 128;

        /// <summary>ProjectName 최대 길이.</summary>
        public const int MaxProjectNameLength = 64;

        /// <summary>ProjectName 필드 바이트 크기 (Header 내).</summary>
        public const int ProjectNameFieldSize = 128;

        /// <summary>IPC 이름 프리픽스.</summary>
        public const string IpcPrefix = "UEB_";

        /// <summary>Mutex 접미사.</summary>
        public const string MutexSuffix = "_Mtx";

        /// <summary>Snapshot Event 접미사.</summary>
        public const string SnapshotEventSuffix = "_SnapshotEvt";

        /// <summary>Stream Event 접미사.</summary>
        public const string StreamEventSuffix = "_StreamEvt";

        /// <summary>ProjectName에서 치환되어야 하는 문자.</summary>
        public static readonly char[] InvalidProjectNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        #endregion

        #region Functions

        /// <summary>
        /// 프로젝트 이름과 프로세스 ID로 MMF 이름을 생성한다.
        /// </summary>
        /// <param name="projectName">Unreal 프로젝트 이름.</param>
        /// <param name="pid">Editor 프로세스 ID.</param>
        /// <returns>MMF 이름 (예: "UEB_MyGame_12345").</returns>
        public static string BuildMmfName(string projectName, uint pid)
        {
            var sanitized = SanitizeProjectName(projectName);
            return $"{IpcPrefix}{sanitized}_{pid}";
        }

        /// <summary>
        /// MMF 이름으로부터 Mutex 이름을 생성한다.
        /// </summary>
        public static string BuildMutexName(string mmfName) => mmfName + MutexSuffix;

        /// <summary>
        /// MMF 이름으로부터 Snapshot Event 이름을 생성한다.
        /// </summary>
        public static string BuildSnapshotEventName(string mmfName) => mmfName + SnapshotEventSuffix;

        /// <summary>
        /// MMF 이름으로부터 Stream Event 이름을 생성한다.
        /// </summary>
        public static string BuildStreamEventName(string mmfName) => mmfName + StreamEventSuffix;

        /// <summary>
        /// 프로젝트 이름에서 허용되지 않는 문자를 '_'로 치환하고 최대 길이를 제한한다.
        /// </summary>
        /// <param name="raw">원본 프로젝트 이름.</param>
        /// <returns>안전한 프로젝트 이름.</returns>
        public static string SanitizeProjectName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "Unknown";

            var chars = raw.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(InvalidProjectNameChars, chars[i]) >= 0)
                    chars[i] = '_';
            }

            var result = new string(chars);
            if (result.Length > MaxProjectNameLength)
                result = result[..MaxProjectNameLength];

            return result;
        }

        /// <summary>
        /// 프로토콜 버전 값에서 Major 버전을 추출한다.
        /// </summary>
        public static uint GetMajorVersion(uint version) => version / 1000;

        /// <summary>
        /// 프로토콜 버전 값에서 Minor 버전을 추출한다.
        /// </summary>
        public static uint GetMinorVersion(uint version) => version % 1000;

        /// <summary>
        /// 두 버전의 Major 버전이 호환되는지 확인한다.
        /// </summary>
        public static bool IsMajorCompatible(uint readerVersion, uint writerVersion)
            => GetMajorVersion(readerVersion) == GetMajorVersion(writerVersion);

        #endregion
    }
}

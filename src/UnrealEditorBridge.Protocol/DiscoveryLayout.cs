namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// Discovery MMF의 Header 및 Entry 필드 오프셋을 정의한다.
    /// C++ BridgeHeaderLayout의 Discovery 영역과 동일한 레이아웃을 유지해야 한다.
    /// </summary>
    public static class DiscoveryLayout
    {
        #region Const Fields – Header

        /// <summary>Discovery Magic 오프셋 (uint32, 4 bytes). "UEBD" = 0x55454244.</summary>
        public const int MagicOffset = 0x00;

        /// <summary>EntryCount 오프셋 (uint32, 4 bytes). 현재 등록된 인스턴스 수.</summary>
        public const int EntryCountOffset = 0x04;

        #endregion

        #region Const Fields – Entry (상대 오프셋)

        /// <summary>ProcessId 오프셋 (uint32, 4 bytes). 엔트리 시작 기준.</summary>
        public const int EntryProcessIdOffset = 0x00;

        /// <summary>RegisteredAt 오프셋 (int64, 8 bytes). UTC Ticks.</summary>
        public const int EntryRegisteredAtOffset = 0x08;

        /// <summary>LastHeartbeat 오프셋 (int64, 8 bytes). UTC Ticks.</summary>
        public const int EntryLastHeartbeatOffset = 0x10;

        /// <summary>ProjectName 오프셋 (char[128], UTF-8). 엔트리 내 상대.</summary>
        public const int EntryProjectNameOffset = 0x20;

        /// <summary>ProjectName 필드 크기 (바이트).</summary>
        public const int EntryProjectNameSize = 128;

        /// <summary>MmfName 오프셋 (char[256], UTF-8). 엔트리 내 상대.</summary>
        public const int EntryMmfNameOffset = 0xA0;

        /// <summary>MmfName 필드 크기 (바이트).</summary>
        public const int EntryMmfNameSize = 256;

        #endregion

        #region Functions

        /// <summary>
        /// Discovery MMF 내에서 특정 엔트리의 절대 오프셋을 계산한다.
        /// </summary>
        /// <param name="entryIndex">엔트리 인덱스 (0-based).</param>
        /// <returns>MMF 내 절대 오프셋.</returns>
        public static int GetEntryAbsoluteOffset(int entryIndex)
            => ProtocolConstants.DiscoveryHeaderSize + entryIndex * ProtocolConstants.DiscoveryEntrySize;

        #endregion
    }
}

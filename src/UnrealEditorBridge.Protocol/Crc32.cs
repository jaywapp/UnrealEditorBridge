namespace UnrealEditorBridge.Protocol
{

    /// <summary>
    /// CRC32 체크섬 계산 유틸리티.
    /// Snapshot 데이터 무결성 검증에 사용한다. IEEE 802.3 (표준 CRC32) 다항식을 사용한다.
    /// </summary>
    public static class Crc32
    {
        #region Const Fields

        private const uint Polynomial = 0xEDB88320u;

        #endregion

        #region Internal Fields

        private static readonly uint[] Table = GenerateTable();

        #endregion

        #region Functions

        /// <summary>
        /// 바이트 배열의 CRC32 체크섬을 계산한다.
        /// </summary>
        /// <param name="data">체크섬을 계산할 데이터.</param>
        /// <returns>CRC32 체크섬 값.</returns>
        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in data)
            {
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFFu;
        }

        /// <summary>
        /// 바이트 배열의 CRC32 체크섬을 계산한다 (배열 버전).
        /// </summary>
        /// <param name="data">체크섬을 계산할 데이터.</param>
        /// <param name="offset">시작 오프셋.</param>
        /// <param name="length">데이터 길이.</param>
        /// <returns>CRC32 체크섬 값.</returns>
        public static uint Compute(byte[] data, int offset, int length)
            => Compute(data.AsSpan(offset, length));

        private static uint[] GenerateTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ Polynomial;
                    else
                        entry >>= 1;
                }
                table[i] = entry;
            }
            return table;
        }

        #endregion
    }
}

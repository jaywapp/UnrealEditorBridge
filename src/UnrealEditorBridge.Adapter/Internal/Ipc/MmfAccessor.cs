using System.IO.MemoryMappedFiles;

namespace UnrealEditorBridge.Adapter.Internal.Ipc
{

    /// <summary>
    /// Memory-Mapped File 열기/닫기/읽기를 캡슐화한다.
    /// Named MMF를 열고 뷰를 매핑하여 바이트 단위 접근을 제공한다.
    /// Dispose 시 모든 핸들을 해제한다.
    /// </summary>
    internal sealed class MmfAccessor : IDisposable
    {
        #region Internal Fields

        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _view;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>MMF가 열려 있는지 여부.</summary>
        public bool IsOpen => _mmf != null && _view != null;

        /// <summary>매핑된 뷰의 전체 크기.</summary>
        public long Capacity => _view?.Capacity ?? 0;

        #endregion

        #region Functions

        /// <summary>
        /// 지정된 이름의 기존 Named MMF를 연다.
        /// </summary>
        /// <param name="mmfName">MMF 이름.</param>
        /// <returns>성공 시 true, MMF가 존재하지 않으면 false.</returns>
        public bool TryOpen(string mmfName)
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.Read);
                _view = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                Close();
                return false;
            }
        }

        /// <summary>
        /// MMF 뷰에서 지정된 오프셋부터 바이트 배열을 읽어 복사한다.
        /// Mutex 보호 하에 호출해야 한다 (copy-out 전략).
        /// </summary>
        /// <param name="offset">읽기 시작 오프셋.</param>
        /// <param name="count">읽을 바이트 수.</param>
        /// <returns>복사된 바이트 배열.</returns>
        /// <exception cref="InvalidOperationException">MMF가 열려 있지 않은 경우.</exception>
        public byte[] ReadBytes(long offset, int count)
        {
            if (_view == null)
                throw new InvalidOperationException("MMF is not open.");

            var buffer = new byte[count];
            _view.ReadArray(offset, buffer, 0, count);
            return buffer;
        }

        /// <summary>
        /// MMF 뷰에서 지정된 오프셋부터 바이트를 기존 버퍼에 읽는다.
        /// </summary>
        /// <param name="offset">읽기 시작 오프셋.</param>
        /// <param name="buffer">대상 버퍼.</param>
        /// <param name="bufferOffset">버퍼 내 시작 오프셋.</param>
        /// <param name="count">읽을 바이트 수.</param>
        public void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (_view == null)
                throw new InvalidOperationException("MMF is not open.");

            _view.ReadArray(offset, buffer, bufferOffset, count);
        }

        /// <summary>
        /// MMF를 닫고 모든 핸들을 해제한다.
        /// </summary>
        public void Close()
        {
            _view?.Dispose();
            _view = null;
            _mmf?.Dispose();
            _mmf = null;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }

        #endregion
    }
}

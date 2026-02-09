using UnrealEditorBridge.Adapter.Internal;

namespace UnrealEditorBridge.Adapter
{

    /// <summary>
    /// <see cref="IBridgeClient"/> 인스턴스를 생성하는 팩토리.
    /// </summary>
    public static class BridgeClientFactory
    {
        #region Functions

        /// <summary>
        /// 기본 옵션으로 <see cref="IBridgeClient"/>를 생성한다.
        /// </summary>
        /// <returns>새 IBridgeClient 인스턴스.</returns>
        public static IBridgeClient Create()
            => new BridgeClient(new BridgeClientOptions());

        /// <summary>
        /// 지정된 옵션으로 <see cref="IBridgeClient"/>를 생성한다.
        /// </summary>
        /// <param name="options">클라이언트 옵션.</param>
        /// <returns>새 IBridgeClient 인스턴스.</returns>
        public static IBridgeClient Create(BridgeClientOptions options)
            => new BridgeClient(options);

        #endregion
    }
}

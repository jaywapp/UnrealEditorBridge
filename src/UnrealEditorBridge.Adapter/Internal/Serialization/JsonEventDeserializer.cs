using System.Text.Json;
using System.Text.Json.Serialization;
using UnrealEditorBridge.Adapter.Models;
using UnrealEditorBridge.Protocol;

namespace UnrealEditorBridge.Adapter.Internal.Serialization
{

    /// <summary>
    /// Event 슬롯의 JSON 페이로드를 <see cref="AssetEvent"/>로 역직렬화한다.
    /// </summary>
    internal static class JsonEventDeserializer
    {
        #region Internal Fields

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Functions

        /// <summary>
        /// 이벤트 슬롯 바이트 데이터에서 <see cref="AssetEvent"/>를 생성한다.
        /// 슬롯의 고정 헤더와 JSON 페이로드를 모두 파싱한다.
        /// </summary>
        /// <param name="slotData">슬롯 바이트 데이터.</param>
        /// <returns>파싱된 에셋 이벤트.</returns>
        public static AssetEvent Deserialize(ReadOnlySpan<byte> slotData)
        {
            var seqNum = EventRecordParser.ReadSequenceNumber(slotData);
            var eventType = EventRecordParser.ReadEventType(slotData);
            var payloadJson = EventRecordParser.ReadPayloadJson(slotData);

            string objectPath = string.Empty;
            string? assetName = null;
            string? className = null;
            string? oldObjectPath = null;
            string? oldAssetName = null;

            if (!string.IsNullOrEmpty(payloadJson))
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<EventPayloadDto>(payloadJson, Options);
                    if (dto != null)
                    {
                        objectPath = dto.ObjectPath ?? dto.NewObjectPath ?? string.Empty;
                        assetName = dto.AssetName ?? dto.NewAssetName;
                        className = dto.ClassName;
                        oldObjectPath = dto.OldObjectPath;
                        oldAssetName = dto.OldAssetName;
                    }
                }
                catch
                {
                    // JSON 파싱 실패 시 기본값 유지
                }
            }

            return new AssetEvent
            {
                SequenceNumber = seqNum,
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                ObjectPath = objectPath,
                AssetName = assetName,
                ClassName = className,
                OldObjectPath = oldObjectPath,
                OldAssetName = oldAssetName,
                RawPayloadJson = payloadJson
            };
        }

        #endregion

        #region DTO Classes

        private sealed class EventPayloadDto
        {
            [JsonPropertyName("objectPath")]
            public string? ObjectPath { get; set; }

            [JsonPropertyName("newObjectPath")]
            public string? NewObjectPath { get; set; }

            [JsonPropertyName("packagePath")]
            public string? PackagePath { get; set; }

            [JsonPropertyName("assetName")]
            public string? AssetName { get; set; }

            [JsonPropertyName("newAssetName")]
            public string? NewAssetName { get; set; }

            [JsonPropertyName("oldObjectPath")]
            public string? OldObjectPath { get; set; }

            [JsonPropertyName("oldAssetName")]
            public string? OldAssetName { get; set; }

            [JsonPropertyName("className")]
            public string? ClassName { get; set; }
        }

        #endregion
    }
}

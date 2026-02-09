using System.Text.Json;
using System.Text.Json.Serialization;
using UnrealEditorBridge.Adapter.Models;

namespace UnrealEditorBridge.Adapter.Internal.Serialization
{

    /// <summary>
    /// Snapshot JSON 페이로드를 <see cref="AssetSnapshot"/>으로 역직렬화한다.
    /// System.Text.Json 기반이며 snake_case/camelCase 모두 지원한다.
    /// </summary>
    internal static class JsonSnapshotDeserializer
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
        /// JSON 문자열을 <see cref="AssetSnapshot"/>으로 역직렬화한다.
        /// 먼저 고속 직렬화를 시도하고, 실패 시 JsonDocument 기반 개별 파싱으로 폴백한다.
        /// </summary>
        /// <param name="json">Snapshot JSON 문자열.</param>
        /// <param name="snapshotVersion">Header에서 읽은 SnapshotVersion.</param>
        /// <returns>역직렬화된 스냅샷. 전체 파싱 실패 시에도 복구된 에셋을 반환한다.</returns>
        public static AssetSnapshot Deserialize(string json, uint snapshotVersion)
        {
            // 1차: 고속 직렬화 시도
            try
            {
                var dto = JsonSerializer.Deserialize<SnapshotDto>(json, Options);
                if (dto != null)
                    return BuildSnapshot(dto, snapshotVersion);
            }
            catch
            {
                // 2차: JsonDocument 기반 개별 에셋 파싱으로 폴백
            }

            return DeserializeResilient(json, snapshotVersion);
        }

        /// <summary>
        /// JsonDocument를 사용하여 개별 에셋 단위로 파싱한다.
        /// 일부 에셋이 깨진 JSON을 포함해도 나머지 에셋을 복구한다.
        /// </summary>
        private static AssetSnapshot DeserializeResilient(string json, uint snapshotVersion)
        {
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    MaxDepth = 64
                });

                var root = doc.RootElement;
                var timestamp = root.TryGetProperty("timestamp", out var tsEl)
                    ? tsEl.GetDateTime()
                    : DateTime.UtcNow;
                var assetCount = root.TryGetProperty("assetCount", out var acEl)
                    ? acEl.GetInt32()
                    : 0;

                var assets = new List<AssetInfo>(assetCount);

                if (root.TryGetProperty("assets", out var assetsEl) &&
                    assetsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var assetEl in assetsEl.EnumerateArray())
                    {
                        try
                        {
                            assets.Add(ParseAssetElement(assetEl));
                        }
                        catch
                        {
                            // 개별 에셋 파싱 실패 → 건너뛰고 계속
                        }
                    }
                }

                return new AssetSnapshot
                {
                    Timestamp = timestamp,
                    AssetCount = assetCount,
                    Assets = assets,
                    Version = snapshotVersion
                };
            }
            catch
            {
                return CreateEmptySnapshot(snapshotVersion);
            }
        }

        /// <summary>
        /// JsonElement에서 개별 AssetInfo를 파싱한다.
        /// </summary>
        private static AssetInfo ParseAssetElement(JsonElement el)
        {
            var tags = new Dictionary<string, string>();
            if (el.TryGetProperty("tags", out var tagsEl) &&
                tagsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in tagsEl.EnumerateObject())
                {
                    try
                    {
                        tags[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }
                    catch
                    {
                        // 개별 태그 파싱 실패 → 건너뛰기
                    }
                }
            }

            var hard = Array.Empty<string>();
            var soft = Array.Empty<string>();
            if (el.TryGetProperty("dependencies", out var depsEl) &&
                depsEl.ValueKind == JsonValueKind.Object)
            {
                if (depsEl.TryGetProperty("hard", out var hardEl) &&
                    hardEl.ValueKind == JsonValueKind.Array)
                {
                    hard = hardEl.EnumerateArray()
                        .Select(e => e.GetString() ?? string.Empty)
                        .ToArray();
                }
                if (depsEl.TryGetProperty("soft", out var softEl) &&
                    softEl.ValueKind == JsonValueKind.Array)
                {
                    soft = softEl.EnumerateArray()
                        .Select(e => e.GetString() ?? string.Empty)
                        .ToArray();
                }
            }

            return new AssetInfo
            {
                ObjectPath = el.TryGetProperty("objectPath", out var op) ? op.GetString() ?? string.Empty : string.Empty,
                PackagePath = el.TryGetProperty("packagePath", out var pp) ? pp.GetString() ?? string.Empty : string.Empty,
                AssetName = el.TryGetProperty("assetName", out var an) ? an.GetString() ?? string.Empty : string.Empty,
                ClassName = el.TryGetProperty("className", out var cn) ? cn.GetString() ?? string.Empty : string.Empty,
                Tags = tags,
                Dependencies = new AssetDependencyInfo { Hard = hard, Soft = soft }
            };
        }

        private static AssetSnapshot BuildSnapshot(SnapshotDto dto, uint snapshotVersion)
        {
            var assets = new List<AssetInfo>(dto.AssetCount);
            if (dto.Assets != null)
            {
                foreach (var a in dto.Assets)
                {
                    assets.Add(new AssetInfo
                    {
                        ObjectPath = a.ObjectPath ?? string.Empty,
                        PackagePath = a.PackagePath ?? string.Empty,
                        AssetName = a.AssetName ?? string.Empty,
                        ClassName = a.ClassName ?? string.Empty,
                        Tags = a.Tags ?? new Dictionary<string, string>(),
                        Dependencies = new AssetDependencyInfo
                        {
                            Hard = (IReadOnlyList<string>?)a.Dependencies?.Hard ?? Array.Empty<string>(),
                            Soft = (IReadOnlyList<string>?)a.Dependencies?.Soft ?? Array.Empty<string>()
                        }
                    });
                }
            }

            return new AssetSnapshot
            {
                Timestamp = dto.Timestamp,
                AssetCount = dto.AssetCount,
                Assets = assets,
                Version = snapshotVersion
            };
        }

        private static AssetSnapshot CreateEmptySnapshot(uint version) => new()
        {
            Timestamp = DateTime.UtcNow,
            AssetCount = 0,
            Assets = Array.Empty<AssetInfo>(),
            Version = version
        };

        #endregion

        #region DTO Classes

        private sealed class SnapshotDto
        {
            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }

            [JsonPropertyName("assetCount")]
            public int AssetCount { get; set; }

            [JsonPropertyName("assets")]
            public List<AssetDto>? Assets { get; set; }
        }

        private sealed class AssetDto
        {
            [JsonPropertyName("objectPath")]
            public string? ObjectPath { get; set; }

            [JsonPropertyName("packagePath")]
            public string? PackagePath { get; set; }

            [JsonPropertyName("assetName")]
            public string? AssetName { get; set; }

            [JsonPropertyName("className")]
            public string? ClassName { get; set; }

            [JsonPropertyName("tags")]
            public Dictionary<string, string>? Tags { get; set; }

            [JsonPropertyName("dependencies")]
            public DependencyDto? Dependencies { get; set; }
        }

        private sealed class DependencyDto
        {
            [JsonPropertyName("hard")]
            public List<string>? Hard { get; set; }

            [JsonPropertyName("soft")]
            public List<string>? Soft { get; set; }
        }

        #endregion
    }
}

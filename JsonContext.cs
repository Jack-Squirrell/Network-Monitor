using System.Text.Json.Serialization;
using System.Collections.Generic;

/// <summary>
/// Source-generation context for System.Text.Json. When publishing
/// trimmed or single-file builds the runtime JSON serializer requires
/// compile-time metadata for types used by endpoints. This file
/// explicitly registers the POCO types used by the API so serialization
/// works correctly in Release/Trimmed builds.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(HostStatus))]
[JsonSerializable(typeof(IEnumerable<HostStatus>))]
[JsonSerializable(typeof(IEnumerable<string>))]
[JsonSerializable(typeof(MonitoringTarget))]
[JsonSerializable(typeof(IEnumerable<MonitoringTarget>))]
internal partial class JsonContext : JsonSerializerContext
{
}

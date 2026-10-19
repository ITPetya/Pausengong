namespace Pausengong.Core.Models;

/// <summary>
/// One node's periodic self-report of its smoothed BLE RSSI to every peer it currently hears.
/// Published non-retained on <see cref="Mqtt.TopicSchema.NodeBleNeighbors"/> — see
/// HANDOFF.md "Phase 7 replacement: BLE neighborhood clustering".
/// </summary>
public sealed record NeighborReport(string ObserverNodeId, IReadOnlyDictionary<string, double> ObservedRssiByNodeId, DateTimeOffset Timestamp);

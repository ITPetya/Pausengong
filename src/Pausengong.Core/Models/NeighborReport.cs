namespace Pausengong.Core.Models;

/// <summary>
/// One node's periodic self-report of its smoothed proximity signal to every peer it currently
/// hears. Shared by both room-detection tracks (see HANDOFF.md "Phase 7 replacement") — the
/// signal is BLE RSSI in dBm on <see cref="Mqtt.TopicSchema.NodeBleNeighbors"/>, or an ultrasonic
/// amplitude ratio on <see cref="Mqtt.TopicSchema.NodeUltrasonicNeighbors"/>. The two are never
/// mixed into one clustering pass — different scales, different units — the topic a report
/// arrived on is what tells a subscriber which track it belongs to.
/// </summary>
public sealed record NeighborReport(string ObserverNodeId, IReadOnlyDictionary<string, double> ObservedSignalByNodeId, DateTimeOffset Timestamp);

namespace Pausengong.Core.Election;

/// <summary>One node's fixed place in the Chef succession order, plus its current network address.</summary>
public sealed record SuccessionEntry(
    string NodeId,
    string HostName,
    string Address,
    int MqttPort,
    DateTimeOffset PriorityTimestamp);

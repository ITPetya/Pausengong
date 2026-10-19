namespace Pausengong.Core.Mqtt;

/// <summary>Typed constants/helpers for the fixed Pausengong MQTT topic tree.</summary>
public static class TopicSchema
{
    public const string Root = "pausengong";

    public static string NodeStatus(string nodeId) => $"{Root}/nodes/{nodeId}/status";
    public static string NodeOnline(string nodeId) => $"{Root}/nodes/{nodeId}/online";

    /// <summary>Non-retained per-node smoothed BLE-RSSI-to-neighbors map, published periodically
    /// by every node (see Neighbors/ — BLE neighborhood clustering, HANDOFF.md "Phase 7 replacement").</summary>
    public static string NodeBleNeighbors(string nodeId) => $"{Root}/nodes/{nodeId}/ble-neighbors";

    /// <summary>Non-retained per-node smoothed ultrasonic-amplitude-to-neighbors map, same shape
    /// and cadence as <see cref="NodeBleNeighbors"/> but for Track B (see HANDOFF.md "Track B:
    /// ultrasonic presence sensing"). Never aggregated together with the BLE topic.</summary>
    public static string NodeUltrasonicNeighbors(string nodeId) => $"{Root}/nodes/{nodeId}/ultrasonic-neighbors";

    public const string NodeStatusWildcard = $"{Root}/nodes/+/status";
    public const string NodeOnlineWildcard = $"{Root}/nodes/+/online";
    public const string NodeBleNeighborsWildcard = $"{Root}/nodes/+/ble-neighbors";
    public const string NodeUltrasonicNeighborsWildcard = $"{Root}/nodes/+/ultrasonic-neighbors";

    /// <summary>Non-retained, Chef-published round-robin transmit schedule for Track B (ultrasonic
    /// presence sensing) — every node needs this to know when it's its own turn to chirp vs.
    /// everyone else's turn to listen. See HANDOFF.md "Track B", step 2 (collision handling).</summary>
    public const string UltrasonicSchedule = $"{Root}/ultrasonic/schedule";

    /// <summary>Pulls the <c>{nodeId}</c> segment out of any <c>pausengong/nodes/{nodeId}/...</c> topic.</summary>
    public static string? ParseNodeIdFromNodesTopic(string topic)
    {
        var parts = topic.Split('/');
        return parts.Length >= 3 && parts[0] == Root && parts[1] == "nodes" ? parts[2] : null;
    }

    public const string ConfigSchedule = $"{Root}/config/schedule";
    public const string ConfigRooms = $"{Root}/config/rooms";
    public const string ConfigRoomMode = $"{Root}/config/room-mode";
    public const string ConfigTestMode = $"{Root}/config/test-mode";

    /// <summary>Non-retained "give me your current config" signal, published on (re)connect.</summary>
    public const string ConfigRequest = $"{Root}/config/request";

    public const string ElectionSuccession = $"{Root}/election/succession";
    public const string ElectionChief = $"{Root}/election/chief";

    public static string RingTarget(string nodeId) => $"{Root}/ring/target/{nodeId}";
    public static string RingConfirm(string nodeId) => $"{Root}/ring/confirm/{nodeId}";

    public const string RingConfirmWildcard = $"{Root}/ring/confirm/+";

    /// <summary>Non-retained "who's currently drawn per room" broadcast from the Chef — every
    /// node subscribes so its UI can show the pick without running the draw itself.</summary>
    public const string RingDrawPreview = $"{Root}/ring/draw-preview";

    /// <summary>Pulls the <c>{nodeId}</c> segment out of a <c>pausengong/ring/confirm/{nodeId}</c> topic.</summary>
    public static string? ParseNodeIdFromRingConfirmTopic(string topic)
    {
        var parts = topic.Split('/');
        return parts.Length >= 4 && parts[0] == Root && parts[1] == "ring" && parts[2] == "confirm" ? parts[3] : null;
    }

    /// <summary>Wildcard subscription covering every per-node topic, for the Chef.</summary>
    public const string AllNodesWildcard = $"{Root}/nodes/+/#";
}

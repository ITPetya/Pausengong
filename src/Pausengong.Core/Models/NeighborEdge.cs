namespace Pausengong.Core.Models;

/// <summary>
/// Symmetrized BLE RSSI between one unordered pair of nodes, produced by
/// <see cref="Neighbors.NeighborGraphBuilder"/>. RSSI is in dBm (negative; closer to zero = stronger).
/// </summary>
public sealed record NeighborEdge(string NodeIdA, string NodeIdB, double SymmetrizedRssi, bool IsBidirectional);

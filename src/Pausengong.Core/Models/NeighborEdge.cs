namespace Pausengong.Core.Models;

/// <summary>
/// Symmetrized proximity signal between one unordered pair of nodes, produced by
/// <see cref="Neighbors.NeighborGraphBuilder"/>. Unit depends on which track produced it (BLE
/// RSSI in dBm, negative, closer to zero = stronger; or an ultrasonic amplitude ratio) — see
/// <see cref="NeighborReport"/>. Never compare or combine edges from different tracks.
/// </summary>
public sealed record NeighborEdge(string NodeIdA, string NodeIdB, double SymmetrizedSignal, bool IsBidirectional);

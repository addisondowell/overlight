namespace Overlight.App.ReadLayer;

/// <summary>
/// Immutable, read-only view of a single top-level window at the moment
/// it was enumerated. Nothing in the read layer ever writes back to the
/// window it describes.
/// </summary>
public sealed record WindowSnapshot(
    nint Handle,
    string Title,
    int ProcessId,
    bool IsVisible,
    bool IsCloaked);

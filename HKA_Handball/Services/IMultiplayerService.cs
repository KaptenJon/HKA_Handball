namespace HKA_Handball.Services;

/// <summary>
/// Represents a message exchanged between devices in a multiplayer game.
/// </summary>
public class MultiplayerMessage
{
    /// <summary>Type of the message (e.g., "input", "state", "goal").</summary>
    public string Type { get; set; } = "";

    /// <summary>JSON-serialized payload.</summary>
    public string Payload { get; set; } = "";
}

/// <summary>
/// Connection state for the multiplayer service.
/// </summary>
public enum MultiplayerConnectionState
{
    Disconnected,
    Searching,
    Connecting,
    Connected
}

/// <summary>
/// Abstraction for local multiplayer communication (Bluetooth, Wi-Fi Direct, etc.).
/// Implementations are platform-specific.
/// </summary>
public interface IMultiplayerService
{
    /// <summary>Current connection state.</summary>
    MultiplayerConnectionState ConnectionState { get; }

    /// <summary>Raised when the connection state changes.</summary>
    event EventHandler<MultiplayerConnectionState>? StateChanged;

    /// <summary>Raised when a message is received from the remote device.</summary>
    event EventHandler<MultiplayerMessage>? MessageReceived;

    /// <summary>
    /// Start advertising this device as a game host.
    /// Other devices can discover and connect to us.
    /// </summary>
    Task StartHostingAsync(CancellationToken ct = default);

    /// <summary>
    /// Start searching for a nearby host to join.
    /// </summary>
    Task StartDiscoveryAsync(CancellationToken ct = default);

    /// <summary>
    /// Send a message to the connected peer.
    /// </summary>
    Task SendAsync(MultiplayerMessage message, CancellationToken ct = default);

    /// <summary>
    /// Disconnect and clean up resources.
    /// </summary>
    Task DisconnectAsync();
}

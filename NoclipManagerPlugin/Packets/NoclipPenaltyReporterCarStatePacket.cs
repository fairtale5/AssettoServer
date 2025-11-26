/// <summary>
/// NoclipPenaltyReporterCarStatePacket - OnlineEvent packet definition for client-to-server communication
/// 
/// Purpose:
/// Defines the packet structure that clients send to the server when reporting car state.
/// This packet is sent by the NoclipPenaltyReporter.lua client plugin.
/// 
/// Packet Key: "NoclipPenaltyReporter_CarState"
/// Direction: Client → Server (one-way communication)
/// 
/// Packet Structure:
/// - wheelsOut (byte): Number of wheels currently off-track (0-4)
/// - isWrongDirection (bool): True if car is facing wrong direction (>45° from AI line)
/// - shouldNoClip (bool): Client's recommendation - true if wheels off-track OR wrong direction
/// - reason (string, 32 chars): Human-readable reason (e.g., "wheels_off_track", "wrong_direction", "wheels_off_track+wrong_direction")
/// 
/// How it works:
/// 1. Client plugin detects conditions and creates packet with car state
/// 2. Client sends packet via ac.OnlineEvent() Lua API
/// 3. Server receives packet and OnNoclipPenaltyReporterCarState() handler is called
/// 4. Server processes packet and requests noclip via NoclipManager if needed
/// </summary>

using AssettoServer.Network.ClientMessages;

namespace NoclipManagerPlugin.Packets;

/// <summary>
/// OnlineEvent packet sent from client to server reporting car state
/// Key must match the client-side Lua plugin's packet key exactly
/// </summary>
[OnlineEvent(Key = "NoclipPenaltyReporter_CarState")]
public class NoclipPenaltyReporterCarStatePacket : OnlineEvent<NoclipPenaltyReporterCarStatePacket>
{
    /// <summary>
    /// Number of wheels currently off-track (0-4)
    /// Client reads this from carl.wheelsOutside Lua API
    /// </summary>
    [OnlineEventField(Name = "wheelsOut")]
    public byte WheelsOut;
    
    /// <summary>
    /// True if car is facing wrong direction (>45° away from AI line direction)
    /// Client calculates this using IsAngledRight() function
    /// </summary>
    [OnlineEventField(Name = "isWrongDirection")]
    public bool IsWrongDirection;
    
    /// <summary>
    /// Client's recommendation: true if no-clip should be enabled
    /// Set to true when wheelsOut > threshold OR isWrongDirection == true
    /// Server uses this to decide whether to request noclip
    /// </summary>
    [OnlineEventField(Name = "shouldNoClip")]
    public bool ShouldNoClip;
    
    /// <summary>
    /// Human-readable reason for the state change
    /// Examples: "wheels_off_track", "wrong_direction", "wheels_off_track+wrong_direction", "conditions_clear"
    /// Used for logging and player notifications
    /// </summary>
    [OnlineEventField(Name = "reason", Size = 32)]
    public string Reason = "";
}


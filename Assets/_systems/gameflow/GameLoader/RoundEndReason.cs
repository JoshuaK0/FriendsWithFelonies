/// <summary>
/// Describes the condition that ended an active round.
/// </summary>
public enum RoundEndReason : byte
{
	None,
	TimeExpired,
	AllRobbersCaptured,
	LootStolen
}

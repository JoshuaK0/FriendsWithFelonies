/// <summary>
/// Implement this on a player component which exposes its server-synchronized
/// team ID. It is used by team-owned cameras and tripwire validation.
/// </summary>
public interface ITeamIdProvider
{
    int TeamId { get; }
}

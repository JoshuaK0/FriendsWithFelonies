using UnityEngine;

/// <summary>
/// Presentation-only receiver placed on the networked player model. FishNet
/// RPCs are owned by RiotShieldItemNetworked, not by this component.
/// </summary>
public sealed class RiotShieldNetworkEnabler : MonoBehaviour
{
    [SerializeField] private GameObject riotShield;

    private void Awake()
    {
        SetShieldEnabled(false);
    }

    public void SetShieldEnabled(bool enabled)
    {
        if (riotShield != null)
            riotShield.SetActive(enabled);
    }
}

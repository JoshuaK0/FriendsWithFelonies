using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Register this on player roots that may be detected by GeigerCounterItem.
/// Keep TeamId updated from your existing team system.
/// </summary>
public sealed class GeigerTarget : MonoBehaviour
{
    private static readonly HashSet<GeigerTarget> ActiveTargets = new();

    [SerializeField] private int teamId = -1;
    [SerializeField] private Transform targetPoint;

    public static IEnumerable<GeigerTarget> Instances => ActiveTargets;
    public int TeamId => teamId;
    public Transform TargetPoint => targetPoint != null ? targetPoint : transform;

    public void SetTeamId(int value)
    {
        teamId = value;
    }

    private void OnEnable()
    {
        ActiveTargets.Add(this);
    }

    private void OnDisable()
    {
        ActiveTargets.Remove(this);
    }
}

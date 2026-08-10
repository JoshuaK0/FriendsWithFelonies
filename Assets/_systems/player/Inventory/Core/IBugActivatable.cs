using UnityEngine;

/// <summary>
/// Optional server-side hook for objects that should react to a deployed bug.
/// </summary>
public interface IBugActivatable
{
    void ActivateFromBug(GameObject bugObject);
}

using UnityEngine;

/// <summary>
/// Optional adapter between JackInTheBoxItem and the project's movement system.
/// Implement this on a player component and assign it to the held item prefab.
/// </summary>
public interface IJackInTheBoxMotor
{
    bool IsCrouching { get; }
    bool HasMovementInput { get; }
    void SetBoxMovementLocked(bool locked);
    void ApplyJackLeap(Vector3 velocityChange);
}

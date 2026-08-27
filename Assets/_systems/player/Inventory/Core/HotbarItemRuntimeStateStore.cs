using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owner-local runtime state shared by held items and inventory-level runtime
/// controllers. This is presentation/gameplay convenience state only;
/// authoritative item state must still live on the server.
/// </summary>
public sealed class HotbarItemRuntimeStateStore : MonoBehaviour
{
    private readonly Dictionary<string, float> floatValues = new();
    private readonly Dictionary<string, int> intValues = new();
    private readonly Dictionary<string, bool> boolValues = new();

    private static string Key(int itemId, string name) => itemId + ":" + name;

    public float GetFloat(int itemId, string name, float fallback)
    {
        return floatValues.TryGetValue(Key(itemId, name), out float value) ? value : fallback;
    }

    public void SetFloat(int itemId, string name, float value)
    {
        floatValues[Key(itemId, name)] = value;
    }

    public int GetInt(int itemId, string name, int fallback)
    {
        return intValues.TryGetValue(Key(itemId, name), out int value) ? value : fallback;
    }

    public void SetInt(int itemId, string name, int value)
    {
        intValues[Key(itemId, name)] = value;
    }

    public bool GetBool(int itemId, string name, bool fallback)
    {
        return boolValues.TryGetValue(Key(itemId, name), out bool value) ? value : fallback;
    }

    public void SetBool(int itemId, string name, bool value)
    {
        boolValues[Key(itemId, name)] = value;
    }
}

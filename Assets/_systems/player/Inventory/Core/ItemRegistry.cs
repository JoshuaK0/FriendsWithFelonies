using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Inventory/Item Registry",
    fileName = "Item Registry")]
public class ItemRegistry : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new();

    private Dictionary<string, ItemDefinition> lookup;

    public int Count => items != null ? items.Count : 0;

    private void OnEnable()
    {
        BuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        BuildLookup();
    }
#endif

    private void BuildLookup()
    {
        lookup = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition item = items[i];
            if (item == null)
                continue;

            string lookupName = item.LookupName;
            if (string.IsNullOrWhiteSpace(lookupName))
            {
                Debug.LogWarning(
                    $"ItemRegistry: Item at index {i} has no lookup name.",
                    this);
                continue;
            }

            if (lookup.ContainsKey(lookupName))
            {
                Debug.LogError(
                    $"ItemRegistry: Duplicate lookup name '{lookupName}'.",
                    this);
                continue;
            }

            lookup.Add(lookupName, item);
        }
    }

    public bool IsValidItemId(int itemId)
    {
        return items != null &&
               itemId >= 0 &&
               itemId < items.Count &&
               items[itemId] != null;
    }

    public ItemDefinition GetItem(int itemId)
    {
        return IsValidItemId(itemId)
            ? items[itemId]
            : null;
    }

    public ItemDefinition GetItem(string lookupName)
    {
        if (string.IsNullOrWhiteSpace(lookupName))
            return null;

        if (lookup == null)
            BuildLookup();

        lookup.TryGetValue(lookupName, out ItemDefinition item);
        return item;
    }

    public T GetItem<T>(string lookupName) where T : ItemDefinition
    {
        return GetItem(lookupName) as T;
    }

    public int GetItemId(string lookupName)
    {
        ItemDefinition item = GetItem(lookupName);
        return GetItemId(item);
    }

    public int GetItemId(ItemDefinition item)
    {
        if (item == null || items == null)
            return -1;

        int itemId = items.IndexOf(item);
        if (itemId >= 0)
            return itemId;

        return GetItemIdByLookup(item.LookupName);
    }

    private int GetItemIdByLookup(string lookupName)
    {
        ItemDefinition item = GetItem(lookupName);
        return item != null ? items.IndexOf(item) : -1;
    }

    public string NameOf(int itemId)
    {
        ItemDefinition item = GetItem(itemId);
        return item != null
            ? item.DisplayName
            : $"Item {itemId}";
    }

    public int MaxStackOf(int itemId)
    {
        ItemDefinition item = GetItem(itemId);
        return item != null ? item.MaxStack : 1;
    }

    public bool AllowsMultipleStacks(int itemId)
    {
        ItemDefinition item = GetItem(itemId);
        return item != null && item.AllowMultipleStacks;
    }

    public bool ConsumeOnEmptyOf(int itemId)
    {
        ItemDefinition item = GetItem(itemId);
        return item == null || item.ConsumeOnEmpty;
    }

    public bool IsDroppable(int itemId)
    {
        ItemDefinition item = GetItem(itemId);
        return item != null && item.IsDroppable;
    }

    public NetworkObject WorldPrefabOf(int itemId)
    {
        return GetItem(itemId)?.WorldPrefab;
    }

    public GameObject HeldPrefabOf(int itemId)
    {
        return GetItem(itemId)?.HeldPrefab;
    }

    public GameObject RemoteHeldPrefabOf(int itemId)
    {
        return GetItem(itemId)?.RemoteHeldPrefab;
    }
}

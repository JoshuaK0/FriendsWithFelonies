using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class StickyBombItem : HotbarHeldItem
{
    [Header("Throwing")]
    [SerializeField] private GameObject viewmodel;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Vector3 throwOffset;

    [Header("Detonation")]
    [SerializeField, Range(0f, 180f)] private float viewAngleThreshold = 45f;
    [SerializeField] private KeyCode detonateAllKey = KeyCode.G;
    [SerializeField] private int aimedDetonateMouseButton = 2;

    [Header("Indicators")]
    [SerializeField] private Transform uiCanvas;
    [SerializeField] private GameObject bombIndicatorPrefab;
    [SerializeField] private Color unarmedColor = Color.yellow;
    [SerializeField] private Color armedColor = Color.red;
    [SerializeField, Min(0f)] private float uiScale = 1f;
    [SerializeField, Min(0f)] private float minUiScale = 0.5f;

    private readonly Dictionary<StickyBombProp, GameObject> trackedIndicators = new();

    private Camera playerCamera;
    private StickyBombItemNetworked networkedCounterpart;
    private bool waitingForFinalThrownBomb;

    private int CurrentCount
    {
        get
        {
            if (Inventory == null)
                return 0;

            HotbarSlot slot = Inventory.GetSelectedSlot();
            if (slot == null || slot.itemId != ItemId)
                return 0;

            return Mathf.Max(0, slot.count);
        }
    }

    private bool IsDetonateOnly => CurrentCount == 0;

    protected override void OnContextInitialized()
    {
        networkedCounterpart = ItemServices != null
            ? ItemServices.GetNetworkedStickyBomb()
            : null;

        playerCamera = MyClient.Instance.PlayerManager.LocalPlayerController
            .GetComponent<PlayerCharacter>()
            .GetServiceLocator()
            .PlayerCamera
            .GetComponent<Camera>();
    }

    protected override void OnEquipped()
    {
        UpdateViewmodel();
    }

    protected override void OnEquippedUpdate()
    {
        UpdateViewmodel();
        UpdateBombIndicators();

        if (!IsDetonateOnly &&
            Input.GetMouseButtonDown(0) &&
            muzzle != null &&
            networkedCounterpart != null)
        {
            networkedCounterpart.RequestThrowStickyBomb(
                muzzle.position + transform.TransformVector(throwOffset),
                muzzle.rotation,
                muzzle.forward);

            Inventory?.ConsumeOneConfirmed(ItemId);

            if (CurrentCount == 0)
                waitingForFinalThrownBomb = true;

            UpdateViewmodel();
        }

        if (Input.GetKeyDown(detonateAllKey))
            networkedCounterpart?.RequestDetonateAllStickyBombs();

        if (Input.GetMouseButtonDown(aimedDetonateMouseButton))
            DetonateAimedBombs();

        UpdateEmptyDetonator();
    }

    protected override void OnUnequipped()
    {
        if (viewmodel != null)
            viewmodel.SetActive(false);

        ClearAllBombIndicators();
    }

    private void UpdateViewmodel()
    {
        if (viewmodel != null)
            viewmodel.SetActive(!IsDetonateOnly);
    }

    private void UpdateEmptyDetonator()
    {
        if (!IsDetonateOnly)
        {
            waitingForFinalThrownBomb = false;
            return;
        }

        bool hasActiveBombs = HasOwnedActiveBombs();

        // The final throw is network-spawned, so there may be a short
        // period where the inventory is at zero before the bomb appears.
        if (waitingForFinalThrownBomb)
        {
            if (hasActiveBombs)
                waitingForFinalThrownBomb = false;

            return;
        }

        if (hasActiveBombs)
            return;

        Inventory?.RemoveEmptyItem(ItemId);
    }

    private bool HasOwnedActiveBombs()
    {
        StickyBombProp[] bombs = FindObjectsOfType<StickyBombProp>();

        for (int i = 0; i < bombs.Length; i++)
        {
            StickyBombProp bomb = bombs[i];

            if (!IsOwnedBomb(bomb) || bomb.IsDetonated())
                continue;

            return true;
        }

        return false;
    }

    private void DetonateAimedBombs()
    {
        if (playerCamera == null || networkedCounterpart == null)
            return;

        StickyBombProp[] bombs = FindObjectsOfType<StickyBombProp>();
        for (int i = 0; i < bombs.Length; i++)
        {
            StickyBombProp bomb = bombs[i];
            if (!IsOwnedBomb(bomb) || !bomb.IsArmed() || bomb.IsDetonated())
                continue;

            Vector3 toBomb = bomb.transform.position - playerCamera.transform.position;
            if (Vector3.Angle(playerCamera.transform.forward, toBomb) <= viewAngleThreshold)
                networkedCounterpart.RequestDetonateStickyBomb(bomb.NetworkObject);
        }
    }

    private void UpdateBombIndicators()
    {
        if (playerCamera == null || uiCanvas == null || bombIndicatorPrefab == null)
        {
            ClearAllBombIndicators();
            return;
        }

        StickyBombProp[] bombs = FindObjectsOfType<StickyBombProp>();
        HashSet<StickyBombProp> visibleBombs = new();

        for (int i = 0; i < bombs.Length; i++)
        {
            StickyBombProp bomb = bombs[i];
            if (!IsOwnedBomb(bomb) || bomb.IsDetonated())
                continue;

            Vector3 toBomb = bomb.transform.position - playerCamera.transform.position;
            float angle = Vector3.Angle(playerCamera.transform.forward, toBomb);
            Vector3 screenPosition = playerCamera.WorldToScreenPoint(bomb.transform.position);

            bool shouldShow = screenPosition.z > 0f && (!bomb.IsArmed() || angle <= viewAngleThreshold);
            if (!shouldShow)
                continue;

            visibleBombs.Add(bomb);
            if (!trackedIndicators.TryGetValue(bomb, out GameObject indicator) || indicator == null)
            {
                indicator = Instantiate(bombIndicatorPrefab, uiCanvas);
                trackedIndicators[bomb] = indicator;
            }

            RectTransform rect = indicator.GetComponent<RectTransform>();
            if (rect != null)
                rect.position = screenPosition;

            float distance = Mathf.Max(0.001f, toBomb.magnitude);
            indicator.transform.localScale = Vector3.one * Mathf.Max(minUiScale, uiScale / distance);

            Image image = indicator.GetComponentInChildren<Image>();
            if (image != null)
            {
                image.color = bomb.IsArmed() ? armedColor : unarmedColor;
                image.fillAmount = bomb.IsArmed() ? 1f : bomb.GetArmPercentage();
            }
        }

        List<StickyBombProp> remove = new();
        foreach (KeyValuePair<StickyBombProp, GameObject> pair in trackedIndicators)
        {
            if (pair.Key == null || !visibleBombs.Contains(pair.Key))
            {
                if (pair.Value != null)
                    Destroy(pair.Value);

                remove.Add(pair.Key);
            }
        }

        for (int i = 0; i < remove.Count; i++)
            trackedIndicators.Remove(remove[i]);
    }

    private bool IsOwnedBomb(StickyBombProp bomb)
    {
        return bomb != null &&
               bomb.NetworkObject != null &&
               Inventory != null &&
               bomb.NetworkObject.Owner == Inventory.Owner;
    }

    private void ClearAllBombIndicators()
    {
        foreach (GameObject indicator in trackedIndicators.Values)
        {
            if (indicator != null)
                Destroy(indicator);
        }

        trackedIndicators.Clear();
    }
}

using UnityEngine;

/// <summary>
/// Explicit player-owned references to item-specific network counterparts.
/// Held items receive this locator from NetHotbarInventory and call the
/// getter for their exact networked item script.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemServiceLocator : MonoBehaviour
{
    [Header("Equipment")]
    [SerializeField] private FlashlightItemNetworked networkedFlashlight;
    [SerializeField] private UVFlashlightItemNetworked NetworkedUVFlashlight;
    [SerializeField] private RiotShieldItemNetworked networkedRiotShield;
    [SerializeField] private GeigerCounterItemNetworked networkedGeigerCounter;
    [SerializeField] private DisguiseItemNetworked networkedDisguise;

    [Header("Placeable Items")]
    [SerializeField] private LadderItemNetworked networkedLadder;
    [SerializeField] private CannonItemNetworked networkedCannon;
    [SerializeField] private BugItemNetworked networkedBug;
    [SerializeField] private DrillItemNetworked networkedDrill;
    [SerializeField] private SandbagItemNetworked networkedSandbag;
    [SerializeField] private StickyCameraItemNetworked networkedStickyCamera;
    [SerializeField] private TripWireItemNetworked networkedTripWire;
    [SerializeField] private TightRopeItemNetworked networkedTightRope;

    [Header("Throwable Items")]
    [SerializeField] private GlowstickItemNetworked networkedGlowstick;
    [SerializeField] private SmokeBombItemNetworked networkedSmokeBomb;
    [SerializeField] private StickyBombItemNetworked networkedStickyBomb;
    [SerializeField] private LootItemNetworked networkedLoot;

    [Header("Weapons")]
    [SerializeField] private GunItemNetworked networkedGun;
    [SerializeField] private JackInTheBoxItemNetworked networkedJackInTheBox;

    public FlashlightItemNetworked GetNetworkedFlashlight()
    {
        return networkedFlashlight;
    }

	public UVFlashlightItemNetworked GetNetworkedUVFlashlight()
	{
		return NetworkedUVFlashlight;
	}

	public RiotShieldItemNetworked GetNetworkedRiotShield()
    {
        return networkedRiotShield;
    }

    public GeigerCounterItemNetworked GetNetworkedGeigerCounter()
    {
        return networkedGeigerCounter;
    }

    public LadderItemNetworked GetNetworkedLadder()
    {
        return networkedLadder;
    }

    public CannonItemNetworked GetNetworkedCannon()
    {
        return networkedCannon;
    }

    public BugItemNetworked GetNetworkedBug()
    {
        return networkedBug;
    }

    public DrillItemNetworked GetNetworkedDrill()
    {
        return networkedDrill;
    }

    public SandbagItemNetworked GetNetworkedSandbag()
    {
        return networkedSandbag;
    }

    public StickyCameraItemNetworked GetNetworkedStickyCamera()
    {
        return networkedStickyCamera;
    }

    public TripWireItemNetworked GetNetworkedTripWire()
    {
        return networkedTripWire;
    }

    public TightRopeItemNetworked GetNetworkedTightRope()
    {
        return networkedTightRope;
    }

    public GlowstickItemNetworked GetNetworkedGlowstick()
    {
        return networkedGlowstick;
    }

    public SmokeBombItemNetworked GetNetworkedSmokeBomb()
    {
        return networkedSmokeBomb;
    }

    public StickyBombItemNetworked GetNetworkedStickyBomb()
    {
        return networkedStickyBomb;
    }

    public GunItemNetworked GetNetworkedGun()
    {
        return networkedGun;
    }

    public JackInTheBoxItemNetworked GetNetworkedJackInTheBox()
    {
        return networkedJackInTheBox;
    }

	public LootItemNetworked GetNetworkedLoot()
	{
		return networkedLoot;
	}

    public DisguiseItemNetworked GetNetworkedDisguise()
    {
        return networkedDisguise;
	}
}

using UnityEngine;

public class HotbarInput : MonoBehaviour
{
	[SerializeField] private NetHotbarInventory hotbar;
	[SerializeField] private NetHotbarDropper dropper;
	[SerializeField] private KeyCode dropKey = KeyCode.Q;

	private void Reset()
	{
		hotbar = GetComponent<NetHotbarInventory>();
		dropper = GetComponent<NetHotbarDropper>();
	}

	private void Update()
	{
		if (hotbar == null || !hotbar.IsOwner)
			return;

		// Pause/death only blocks local player input.
		if (!hotbar.CanProcessPlayerInput)
			return;

		for (int i = 0; i < hotbar.SlotCount && i < 9; i++)
		{
			KeyCode slotKey =
				(KeyCode)((int)KeyCode.Alpha1 + i);

			if (Input.GetKeyDown(slotKey))
			{
				hotbar.SelectSlot(i);
				break;
			}
		}

		float scroll = Input.mouseScrollDelta.y;

		if (scroll != 0f)
			hotbar.SelectNext(scroll > 0f ? 1 : -1);

		if (Input.GetKeyDown(dropKey))
			dropper?.DropOneSelected();
	}
}

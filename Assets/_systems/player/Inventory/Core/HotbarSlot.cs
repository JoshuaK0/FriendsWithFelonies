[System.Serializable]
public class HotbarSlot
{
    public int itemId = -1;
    public int count;

    public bool IsEmpty => itemId < 0;
    public bool IsDepleted => !IsEmpty && count <= 0;

    public void Clear()
    {
        itemId = -1;
        count = 0;
    }
}

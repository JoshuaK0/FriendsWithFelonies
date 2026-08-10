using UnityEngine;

public static class ComponentInterfaceUtility
{
    public static T FindInParents<T>(Component component) where T : class
    {
        if (component == null)
            return null;

        Transform current = component.transform;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is T match)
                    return match;
            }

            current = current.parent;
        }

        return null;
    }

    public static T FindOnGameObject<T>(GameObject gameObject) where T : class
    {
        if (gameObject == null)
            return null;

        MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T match)
                return match;
        }

        return null;
    }

    public static T FindInChildren<T>(GameObject gameObject, bool includeInactive = true) where T : class
    {
        if (gameObject == null)
            return null;

        MonoBehaviour[] behaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T match)
                return match;
        }

        return null;
    }
}

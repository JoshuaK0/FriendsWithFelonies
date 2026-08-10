using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public sealed class BugProp : NetworkBehaviour
{
    private static readonly HashSet<BugProp> ActiveInstances = new();

    [SerializeField, Min(0f)] private float activationRange = 8f;
    [SerializeField] private LayerMask activationMask = ~0;
    [SerializeField, Min(4)] private int maxTargets = 64;

    private Collider[] overlapBuffer;

    public static IEnumerable<BugProp> Instances => ActiveInstances;

    private void Awake()
    {
        overlapBuffer = new Collider[Mathf.Max(4, maxTargets)];
    }

    private void OnEnable()
    {
        ActiveInstances.Add(this);
    }

    private void OnDisable()
    {
        ActiveInstances.Remove(this);
    }

    [Server]
    public void ActivateServer()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            activationRange,
            overlapBuffer,
            activationMask,
            QueryTriggerInteraction.Collide);

        HashSet<GameObject> activatedObjects = new();
        for (int i = 0; i < count; i++)
        {
            Collider overlap = overlapBuffer[i];
            if (overlap == null || overlap.transform.IsChildOf(transform))
                continue;

            GameObject targetObject = overlap.gameObject;
            if (!activatedObjects.Add(targetObject))
                continue;

            IBugActivatable bugActivatable = ComponentInterfaceUtility.FindInParents<IBugActivatable>(overlap);
            if (bugActivatable != null)
            {
                bugActivatable.ActivateFromBug(gameObject);
                continue;
            }

            IInteractable interactable = ComponentInterfaceUtility.FindInParents<IInteractable>(overlap);
            interactable?.Interact(gameObject);
        }

        Despawn();
    }
}

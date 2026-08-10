using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Owner-local thermal post-processing toggle. Renderer features are local to
/// the running client and therefore do not need FishNet replication.
/// </summary>
public sealed class ThermalGoggles : HotbarHeldItem
{
    [SerializeField] private UniversalRendererData rendererData;
    [SerializeField] private string featureName = "Outline1";
    [SerializeField] private GameObject screenFx;

    private ScriptableRendererFeature renderFeature;
    private bool initialFeatureState;

    protected override void OnEquipped()
    {
        renderFeature = FindFeature();
        initialFeatureState = renderFeature != null && renderFeature.isActive;
        ApplyScreenFx(initialFeatureState);
    }

    protected override void OnEquippedUpdate()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (renderFeature == null)
            renderFeature = FindFeature();

        if (renderFeature == null)
            return;

        bool enabled = !renderFeature.isActive;
        renderFeature.SetActive(enabled);
        ApplyScreenFx(enabled);
    }

    protected override void OnUnequipped()
    {
        if (renderFeature != null)
            renderFeature.SetActive(initialFeatureState);

        ApplyScreenFx(false);
    }

    private ScriptableRendererFeature FindFeature()
    {
        if (rendererData == null || rendererData.rendererFeatures == null)
            return null;

        for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
        {
            ScriptableRendererFeature feature = rendererData.rendererFeatures[i];
            if (feature != null && feature.name == featureName)
                return feature;
        }

        Debug.LogWarning($"ThermalGoggles could not find renderer feature '{featureName}'.", this);
        return null;
    }

    private void ApplyScreenFx(bool enabled)
    {
        if (screenFx != null)
            screenFx.SetActive(enabled);
    }
}

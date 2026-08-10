using UnityEngine;

public sealed class StickyCameraManualUpdate : MonoBehaviour
{
    [SerializeField] private Camera updateCamera;
    [SerializeField, Min(0.01f)] private float updateRate = 0.1f;

    private float nextUpdateTime;
    private bool manualRefresh;

    public void ToggleManualRefresh(bool enabled)
    {
        manualRefresh = enabled;
        if (updateCamera != null)
            updateCamera.enabled = !enabled;

        nextUpdateTime = 0f;
    }

    private void Update()
    {
        if (!manualRefresh || updateCamera == null || Time.unscaledTime < nextUpdateTime)
            return;

        nextUpdateTime = Time.unscaledTime + updateRate;
        updateCamera.Render();
    }
}

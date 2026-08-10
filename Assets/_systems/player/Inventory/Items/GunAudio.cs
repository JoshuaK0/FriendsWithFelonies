using UnityEngine;

/// <summary>
/// Optional local audio helper. Network gun audio is handled by GunItemNetworked.
/// </summary>
public sealed class GunAudio : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);

    public void Play(AudioClip clip)
    {
        if (source == null || clip == null)
            return;

        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip);
    }
}

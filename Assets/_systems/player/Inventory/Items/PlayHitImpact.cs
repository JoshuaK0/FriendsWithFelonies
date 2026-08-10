using UnityEngine;

public sealed class PlayHitImpact : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] particles;
    [SerializeField, Min(0f)] private float lifeTime = 1f;

    private void Start()
    {
        if (particles != null)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                    particles[i].Play();
            }
        }

        Destroy(gameObject, lifeTime);
    }
}

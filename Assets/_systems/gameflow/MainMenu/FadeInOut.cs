using UnityEngine;
using Evo.UI;

public class FadeInOut : MonoBehaviour
{
    [SerializeField] UIAnimator animator;
    [SerializeField] string fadeInName;
    [SerializeField] string fadeOutName;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        FadeIn();

	}

    public void FadeIn()
    {
        animator.PlayAnimationGroup(fadeInName);
	}

	public void FadeOut()
	{
		animator.PlayAnimationGroup(fadeOutName);
	}
}

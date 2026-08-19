using UnityEngine;
using Michsky.UI.MTP;

public class RoundEndUI : MonoBehaviour
{
    [SerializeField] StyleManager copsWin;
    [SerializeField] StyleManager RobbersWin;
    void Start()
    {
        GameFlowManager.Instance.OnRoundEnded += FinishRound;

    }

    void FinishRound(int round, RoundEndReason roundEnd)
    {
        if(roundEnd == RoundEndReason.LootStolen)
        {
            PlayRobbersWin();
        }
		if (roundEnd == RoundEndReason.TimeExpired)
		{
			PlayCopsWin();
		}
		if (roundEnd == RoundEndReason.AllRobbersCaptured)
		{
			PlayCopsWin();
		}
	}

    void PlayCopsWin()
    {
        copsWin.Play();
    }

    void PlayRobbersWin()
    {
        RobbersWin.Play();
    }
}

using System.Collections;
using UnityEngine;
using Evo.UI;

public class ShopWindowToggle : MonoBehaviour
{
	[Header("Shop Window")]
	[SerializeField]
	private ModalWindow shopWindow;

	[Header("Shop Music")]
	[SerializeField]
	private AudioSource shopAudioSource;

	[SerializeField]
	private AudioClip[] shopTracks;

	[SerializeField, Range(0f, 1f)]
	private float shopMusicVolume = 1f;

	[SerializeField, Min(0f)]
	private float fadeDuration = 0.5f;

	private Coroutine musicFadeCoroutine;

	private int previousTrackIndex = -1;

	private void Awake()
	{
		if (shopAudioSource == null)
		{
			shopAudioSource =
				gameObject.AddComponent<AudioSource>();
		}

		shopAudioSource.playOnAwake = false;
		shopAudioSource.loop = true;
		shopAudioSource.spatialBlend = 0f;
		shopAudioSource.volume = 0f;
	}

	private void Update()
	{
		if (!Input.GetKeyDown(KeyCode.Tab))
			return;

		if (shopWindow.IsOpen)
		{
			CloseShop();
		}
		else if (
			GameFlowManager.Instance != null &&
			GameFlowManager.Instance.RoundPhase ==
				RoundFlowPhase.Setup)
		{
			OpenShop();
		}
	}

	private void OpenShop()
	{
		shopWindow.Open();

		Cursor.lockState =
			CursorLockMode.Confined;

		Cursor.visible = true;

		/*
		 * Fade out the normal soundtrack.
		 */
		if (SoundtrackController.Instance != null)
		{
			SoundtrackController.Instance
				.SetMutedLocal(true);
		}

		PlayRandomShopTrack();
	}

	private void CloseShop()
	{
		shopWindow.Close();

		Cursor.lockState =
			CursorLockMode.Locked;

		Cursor.visible = false;

		/*
		 * Fade the normal soundtrack back in.
		 */
		if (SoundtrackController.Instance != null)
		{
			SoundtrackController.Instance
				.SetMutedLocal(false);
		}

		FadeOutShopMusic();
	}

	private void PlayRandomShopTrack()
	{
		if (shopTracks == null ||
			shopTracks.Length == 0)
		{
			Debug.LogWarning(
				"No shop music tracks assigned."
			);

			return;
		}

		int trackIndex =
			GetRandomTrackIndex();

		AudioClip clip =
			shopTracks[trackIndex];

		if (clip == null)
			return;

		previousTrackIndex =
			trackIndex;

		if (musicFadeCoroutine != null)
		{
			StopCoroutine(
				musicFadeCoroutine
			);
		}

		shopAudioSource.Stop();

		shopAudioSource.clip =
			clip;

		shopAudioSource.volume =
			0f;

		shopAudioSource.Play();

		musicFadeCoroutine =
			StartCoroutine(
				FadeShopMusic(
					shopMusicVolume,
					false
				)
			);
	}

	private void FadeOutShopMusic()
	{
		if (musicFadeCoroutine != null)
		{
			StopCoroutine(
				musicFadeCoroutine
			);
		}

		musicFadeCoroutine =
			StartCoroutine(
				FadeShopMusic(
					0f,
					true
				)
			);
	}

	private IEnumerator FadeShopMusic(
		float targetVolume,
		bool stopWhenFinished)
	{
		float startVolume =
			shopAudioSource.volume;

		if (fadeDuration <= 0f)
		{
			shopAudioSource.volume =
				targetVolume;

			if (stopWhenFinished)
			{
				shopAudioSource.Stop();
				shopAudioSource.clip = null;
			}

			musicFadeCoroutine = null;

			yield break;
		}

		float elapsed = 0f;

		while (elapsed < fadeDuration)
		{
			elapsed +=
				Time.unscaledDeltaTime;

			float t =
				Mathf.Clamp01(
					elapsed /
					fadeDuration
				);

			shopAudioSource.volume =
				Mathf.Lerp(
					startVolume,
					targetVolume,
					t
				);

			yield return null;
		}

		shopAudioSource.volume =
			targetVolume;

		if (stopWhenFinished)
		{
			shopAudioSource.Stop();
			shopAudioSource.clip = null;
		}

		musicFadeCoroutine = null;
	}

	private int GetRandomTrackIndex()
	{
		if (shopTracks.Length <= 1)
			return 0;

		int index;

		do
		{
			index =
				Random.Range(
					0,
					shopTracks.Length
				);
		}
		while (index == previousTrackIndex);

		return index;
	}
}
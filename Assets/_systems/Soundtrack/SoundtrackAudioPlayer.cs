using System.Collections;
using UnityEngine;

public sealed class SoundtrackAudioPlayer : MonoBehaviour
{
	[Header("Audio Sources")]
	[SerializeField]
	private AudioSource audioSourceA;

	[SerializeField]
	private AudioSource audioSourceB;

	[Header("Local Audio")]
	[SerializeField, Range(0f, 1f)]
	private float musicVolume = 1f;

	[SerializeField, Min(0f)]
	private float muteFadeDuration = 0.5f;

	private Coroutine crossFadeCoroutine;
	private Coroutine muteCoroutine;

	private float sourceAGain;
	private float sourceBGain;

	private float sourceATrackVolume = 1f;
	private float sourceBTrackVolume = 1f;

	private float localMuteVolume = 1f;
	private bool locallyMuted;

	public bool IsLocallyMuted => locallyMuted;

	private void Awake()
	{
		SetupAudioSources();
	}

	public void CrossFadeTo(
		SoundtrackTrack track,
		float playbackTime,
		float duration)
	{
		if (track == null ||
			!track.IsValid)
		{
			return;
		}

		StopCrossFade();

		AudioSource fromSource;
		AudioSource toSource;

		if (sourceAGain >= sourceBGain)
		{
			fromSource = audioSourceA;
			toSource = audioSourceB;
		}
		else
		{
			fromSource = audioSourceB;
			toSource = audioSourceA;
		}

		toSource.Stop();
		toSource.clip = track.Clip;
		toSource.loop = false;

		SetTrackVolume(
			toSource,
			track.Volume
		);

		SetSourceGain(
			toSource,
			0f
		);

		if (track.Clip.length > 0f)
		{
			toSource.time =
				Mathf.Clamp(
					playbackTime,
					0f,
					Mathf.Max(
						0f,
						track.Clip.length - 0.01f
					)
				);
		}

		toSource.Play();

		if (duration <= 0f)
		{
			StopSource(fromSource);

			SetSourceGain(
				toSource,
				1f
			);

			return;
		}

		crossFadeCoroutine =
			StartCoroutine(
				CrossFadeRoutine(
					fromSource,
					toSource,
					duration
				)
			);
	}

	public void FadeOut(float duration)
	{
		StopCrossFade();

		if (duration <= 0f)
		{
			StopBothSources();
			return;
		}

		crossFadeCoroutine =
			StartCoroutine(
				FadeOutRoutine(
					duration
				)
			);
	}

	public void ToggleMuteLocal()
	{
		SetMutedLocal(
			!locallyMuted
		);
	}

	public void SetMutedLocal(bool muted)
	{
		if (locallyMuted == muted)
			return;

		locallyMuted = muted;

		if (muteCoroutine != null)
		{
			StopCoroutine(
				muteCoroutine
			);

			muteCoroutine = null;
		}

		float target =
			muted
				? 0f
				: 1f;

		if (muteFadeDuration <= 0f)
		{
			localMuteVolume = target;
			ApplyVolumes();
			return;
		}

		muteCoroutine =
			StartCoroutine(
				FadeMuteRoutine(
					target
				)
			);
	}

	public void SetMusicVolume(float volume)
	{
		musicVolume =
			Mathf.Clamp01(
				volume
			);

		ApplyVolumes();
	}

	private IEnumerator CrossFadeRoutine(
		AudioSource fromSource,
		AudioSource toSource,
		float duration)
	{
		float elapsed = 0f;

		float fromStart =
			GetSourceGain(
				fromSource
			);

		float toStart =
			GetSourceGain(
				toSource
			);

		while (elapsed < duration)
		{
			elapsed +=
				Time.unscaledDeltaTime;

			float t =
				Mathf.Clamp01(
					elapsed /
					duration
				);

			SetSourceGain(
				fromSource,
				Mathf.Lerp(
					fromStart,
					0f,
					t
				)
			);

			SetSourceGain(
				toSource,
				Mathf.Lerp(
					toStart,
					1f,
					t
				)
			);

			yield return null;
		}

		SetSourceGain(
			fromSource,
			0f
		);

		SetSourceGain(
			toSource,
			1f
		);

		StopSource(fromSource);

		crossFadeCoroutine = null;
	}

	private IEnumerator FadeOutRoutine(
		float duration)
	{
		float startA = sourceAGain;
		float startB = sourceBGain;
		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed +=
				Time.unscaledDeltaTime;

			float t =
				Mathf.Clamp01(
					elapsed /
					duration
				);

			SetSourceGain(
				audioSourceA,
				Mathf.Lerp(
					startA,
					0f,
					t
				)
			);

			SetSourceGain(
				audioSourceB,
				Mathf.Lerp(
					startB,
					0f,
					t
				)
			);

			yield return null;
		}

		StopBothSources();

		crossFadeCoroutine = null;
	}

	private IEnumerator FadeMuteRoutine(
		float target)
	{
		float start =
			localMuteVolume;

		float elapsed = 0f;

		while (elapsed <
			   muteFadeDuration)
		{
			elapsed +=
				Time.unscaledDeltaTime;

			float t =
				Mathf.Clamp01(
					elapsed /
					muteFadeDuration
				);

			localMuteVolume =
				Mathf.Lerp(
					start,
					target,
					t
				);

			ApplyVolumes();

			yield return null;
		}

		localMuteVolume = target;
		ApplyVolumes();

		muteCoroutine = null;
	}

	private void SetupAudioSources()
	{
		if (audioSourceA == null)
		{
			audioSourceA =
				gameObject.AddComponent<AudioSource>();
		}

		if (audioSourceB == null)
		{
			audioSourceB =
				gameObject.AddComponent<AudioSource>();
		}

		ConfigureAudioSource(
			audioSourceA
		);

		ConfigureAudioSource(
			audioSourceB
		);

		sourceAGain = 0f;
		sourceBGain = 0f;

		sourceATrackVolume = 1f;
		sourceBTrackVolume = 1f;

		ApplyVolumes();
	}

	private void ConfigureAudioSource(
		AudioSource source)
	{
		source.playOnAwake = false;
		source.loop = false;
		source.spatialBlend = 0f;
		source.volume = 0f;
	}

	private void StopCrossFade()
	{
		if (crossFadeCoroutine == null)
			return;

		StopCoroutine(
			crossFadeCoroutine
		);

		crossFadeCoroutine = null;
	}

	private void StopBothSources()
	{
		StopSource(audioSourceA);
		StopSource(audioSourceB);
	}

	private void StopSource(AudioSource source)
	{
		if (source == null)
			return;

		source.Stop();
		source.clip = null;

		SetSourceGain(
			source,
			0f
		);

		SetTrackVolume(
			source,
			1f
		);
	}

	private void SetSourceGain(
		AudioSource source,
		float gain)
	{
		gain =
			Mathf.Clamp01(
				gain
			);

		if (source == audioSourceA)
		{
			sourceAGain = gain;
		}
		else if (source == audioSourceB)
		{
			sourceBGain = gain;
		}

		ApplyVolumes();
	}

	private float GetSourceGain(
		AudioSource source)
	{
		if (source == audioSourceA)
			return sourceAGain;

		if (source == audioSourceB)
			return sourceBGain;

		return 0f;
	}

	private void SetTrackVolume(
		AudioSource source,
		float volume)
	{
		volume =
			Mathf.Clamp01(
				volume
			);

		if (source == audioSourceA)
		{
			sourceATrackVolume =
				volume;
		}
		else if (source == audioSourceB)
		{
			sourceBTrackVolume =
				volume;
		}

		ApplyVolumes();
	}

	private void ApplyVolumes()
	{
		float multiplier =
			musicVolume *
			localMuteVolume;

		if (audioSourceA != null)
		{
			audioSourceA.volume =
				sourceAGain *
				sourceATrackVolume *
				multiplier;
		}

		if (audioSourceB != null)
		{
			audioSourceB.volume =
				sourceBGain *
				sourceBTrackVolume *
				multiplier;
		}
	}
}

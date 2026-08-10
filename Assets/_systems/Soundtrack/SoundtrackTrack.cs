using System;
using UnityEngine;

[Serializable]
public sealed class SoundtrackTrack
{
	[SerializeField]
	private AudioClip clip;

	[SerializeField, Range(0f, 1f)]
	private float volume = 1f;

	public AudioClip Clip => clip;
	public float Volume => volume;
	public bool IsValid => clip != null;
}

[Serializable]
public sealed class SoundtrackPlaylist
{
	[SerializeField]
	private SoundtrackTrack[] tracks = new SoundtrackTrack[0];

	public int Count => tracks?.Length ?? 0;

	public SoundtrackTrack GetTrack(int index)
	{
		if (tracks == null ||
			index < 0 ||
			index >= tracks.Length)
		{
			return null;
		}

		return tracks[index];
	}

	public bool HasValidTracks()
	{
		if (tracks == null)
			return false;

		for (int i = 0; i < tracks.Length; i++)
		{
			if (tracks[i] != null &&
				tracks[i].IsValid)
			{
				return true;
			}
		}

		return false;
	}
}

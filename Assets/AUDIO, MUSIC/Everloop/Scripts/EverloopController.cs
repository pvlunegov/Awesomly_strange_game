﻿#undef EVERLOOP_VERBOSE

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Everloop;

public class EverloopController : MonoBehaviour {

	// Volume of all tracks in one setting.
	public float volume {
		get {
			return _volume;
		}
		set {
			_volume = value;
			
			// Set volume of all layers.
			if (_tracks != null) {
				for (int i = 0; i < _tracks.Count; ++i) {
					_tracks[i].volume = _trackVolumes[i] * _volume;
				}
			}
		}
	}
	private float _volume = 0.8f;

	public bool fadeInOnStart = false;
	public float masterFadeDuration = 1f;

	// Should the script automatically fade in/out tracks to make change the music? If false, all tracks will be played.
	public bool enableAutopilot = false;

	// Only matters when enableAutopilot is true.
	public int numActiveTracks = 3;
	public float trackFadeInDuration = 5f;
	public float trackFadeOutDuration = 5f;
	public float avgFadeTimeout = 15f;
	public float fadeTimeoutVariance = 3f;
	public int trackNumberVariance = 2;

	public List<AudioSource> tracks {
		get {
			return _tracks;
		}
	}

	private List<AudioSource> _tracks;
	private List<float> _trackVolumes;

	private Coroutine _autopilotCoroutine;
	private bool _isAutopilotActive = false;
	private List<AudioSource> _playing;
	private List<AudioSource> _idle;

	public virtual void PlayAll() {
		PlayAll(0);
	}
	
	public virtual void PlayAll(float fadeInDuration, bool ignoreTimeScale = true) {
		RandomizeLocations();

		foreach (AudioSource layer in _tracks) {
			FadeInLayer(layer, fadeInDuration, ignoreTimeScale);
		}
	}

	virtual public void StopAll() {
		StopAutopilot();
		foreach (AudioSource layer in _tracks) {
			layer.Stop();
		}
	}
	
	virtual public void StopAll(float fadeOutDuration, bool ignoreTimeScale = true) {
		StopAutopilot();
		foreach (AudioSource layer in _tracks) {
			FadeOutLayer(layer, fadeOutDuration);
		}
	}

	virtual public void PlayStopAll(bool fadeInOut = true) {
		bool stopFound = false;
		foreach (AudioSource layer in _tracks) {
			stopFound = stopFound || layer.isPlaying;
		}

		if (stopFound) {
			StopAll(fadeInOut? masterFadeDuration : 0);
			StopCoroutine(AutopilotCoroutine());
		} else {
			PlayAll(fadeInOut? masterFadeDuration : 0);
		}
	}

	virtual public void StartAutopilot() {
		StartAutopilot(numActiveTracks, masterFadeDuration, true);
	}

	// If numInitTracks is -1, activeTrackNumber will be used instead.
	virtual public void StartAutopilot(int numInitTracks, float fadeInDuration = 3f, bool ignoreTimeScale = true) {
		// If already in autopilot mode, return.
		if (_isAutopilotActive) {
			return;
		}
		_isAutopilotActive = true;

		// Check autopilot parameters for validity.
		if (numActiveTracks == 0) {
			Debug.LogError("Everloop: Number of active autopilot tracks must be positive.");
			return;
		}

		if (numInitTracks <= 0) {
			numInitTracks = numActiveTracks;
		}

#if EVERLOOP_VERBOSE
		Debug.Log("EVERLOOP: Starting autopilot.");
#endif

		// Initially fill all currently playing layers into the playing array.
		_playing = new List<AudioSource>();
		foreach (var l in _tracks) {
			if (l.isPlaying && !_playing.Contains(l)) {
				_playing.Add(l);
			}
		}
		
		// If nothing is playing yet, play random layers.
		List<AudioSource> tracksRandomized = new List<AudioSource>(_tracks);
		Utils.Shuffle(tracksRandomized);
		if (_playing.Count == 0) {
			int maxIndex = Mathf.Min(numInitTracks, _tracks.Count);
#if EVERLOOP_VERBOSE
			Debug.Log(string.Format("EVERLOOP: Nothing is playing. Starting {0} layers.", maxIndex));
#endif
			for (int i = 0; i < maxIndex; ++i) {
				FadeInLayer(tracksRandomized[i], fadeInDuration, ignoreTimeScale);
				_playing.Add(tracksRandomized[i]);
			}
		}

		// Initially fill the idle layers into the idle array.
		_idle = new List<AudioSource>();
		foreach (var l in _tracks) {
			if (!l.isPlaying && !_idle.Contains(l)) {
				_idle.Add(l);
			}
		}
		Utils.Shuffle(_idle);
		
		_autopilotCoroutine = StartCoroutine(AutopilotCoroutine());
	}

	public virtual void StopAutopilot() {
		if (_autopilotCoroutine != null) {
			StopCoroutine(_autopilotCoroutine);
			_autopilotCoroutine = null;
		}
		_isAutopilotActive = false;
	}

	public virtual void FadeOutAll(float duration = 1f, bool ignoreTimeScale = true) {
		StartCoroutine(FadeMaster(false, duration, ignoreTimeScale));
	}
	
	public virtual void FadeInLayer(AudioSource layer, float duration = 1f, bool ignoreTimeScale = true) {
#if EVERLOOP_VERBOSE
		Debug.Log(string.Format("EVERLOOP: Fading in {0}.", layer.clip.name));
#endif
		StartCoroutine(FadeLayer(layer, true, duration, ignoreTimeScale));
	}
	
	public virtual void FadeOutLayer(AudioSource layer, float duration = 1f, bool ignoreTimeScale = true) {
#if EVERLOOP_VERBOSE
		Debug.Log(string.Format("EVERLOOP: Fading out {0}.", layer.clip.name));
#endif
		StartCoroutine(FadeLayer(layer, false, duration, ignoreTimeScale));
	}
	
	public virtual void RandomizeLocations(bool includePlaying = false) {
		foreach (AudioSource layer in _tracks) {
			if (includePlaying || !layer.isPlaying) {
				layer.time = Random.Range(0, layer.clip.length - 1);
			}
		}
	}
	
	public virtual void PlayRandom(int numTracks, float fadeInDuration, bool ignoreTimeScale = true) {
		int count = 0;
		int index = 0;
		foreach (AudioSource layer in _tracks) {
			if (layer.isPlaying || Random.value < ((numTracks - count) / (float)(_tracks.Count - index))) {
				FadeLayer(layer, true, fadeInDuration, ignoreTimeScale);
				count++;
			}
			index++;
		}
	}

	void Start() {
		_tracks = new List<AudioSource>(GetComponentsInChildren<AudioSource>());
		numActiveTracks = Mathf.Min(numActiveTracks, _tracks.Count);
		
		// Remember layer volumes.
		_trackVolumes = new List<float>(_tracks.Count);
		for (int i = 0; i < _tracks.Count; ++i) {
			_trackVolumes.Add(_tracks[i].volume);

			// Ensure all tracks are looped.
			_tracks[i].loop = true;
		}
		
		volume = _volume;
		
		if (fadeInOnStart) {
			if (enableAutopilot) {
				StartAutopilot();
			} else {
				PlayAll(masterFadeDuration);
			}
		}
	}

	private IEnumerator AutopilotCoroutine() {
		float timeUntilAction = avgFadeTimeout + Random.Range(-fadeTimeoutVariance, fadeTimeoutVariance);

		while (_isAutopilotActive) {
			yield return new WaitForSeconds(timeUntilAction);

			// Check if autopilot was turned off during the pause.
			if (!_isAutopilotActive) {
				break;
			}

			// Decide type of action.
			float absolute = numActiveTracks + Utils.NormalDistributionRandom(-trackNumberVariance, trackNumberVariance);
			bool isFadeIn = _playing.Count <= 1 || absolute > _playing.Count;
#if EVERLOOP_VERBOSE
			Debug.Log(string.Format("EVERLOOP: Playing: {0}, idle: {1}, action is fade in: {2}.", _playing.Count, 
			                        _idle.Count, isFadeIn));
#endif
			if (isFadeIn) {  // Fade in an idle track.
				// Start one of the first tracks in queue.
				AudioSource layer = Utils.GetOneOfTheFirst(_idle);
				if (layer != null) {
					FadeInLayer(layer, trackFadeInDuration);
					_idle.Remove(layer);
					_playing.Add(layer);
				}
			} else {  // Fade out a playing track.
				AudioSource layer = Utils.GetOneOfTheFirst(_playing);
				if (layer != null) {
					FadeOutLayer(layer, trackFadeOutDuration);
					_playing.Remove(layer);
					_idle.Add(layer);
				}
			}

		}
	}

	private IEnumerator FadeMaster(bool fadeIn, float duration = 1f, bool ignoreTimestep = true) {
		float delta = (fadeIn? 1.0f : -1.0f) / duration;
		
		float targetVolume = _volume;
		if (fadeIn) {
			volume = 0;
		}
		
		var wait = new WaitForEndOfFrame();
		float lastTime = ignoreTimestep? Time.realtimeSinceStartup : Time.timeSinceLevelLoad;
		float currentTime;
		float deltaTime;
		
		while ((fadeIn && volume < targetVolume) || (!fadeIn && volume > 0f)) {
			currentTime = ignoreTimestep? Time.realtimeSinceStartup : Time.timeSinceLevelLoad;
			deltaTime = currentTime - lastTime;
			
			volume = Mathf.Clamp01(volume + delta * deltaTime);
			
			lastTime = currentTime;
			
			yield return wait;
		}
		
		if (fadeIn) {
			volume = targetVolume;
		} else {
			StopAutopilot();
		}
	}
	
	private IEnumerator FadeLayer(AudioSource layer, bool fadeIn, float duration, bool ignoreTimeScale) {
		float delta = (fadeIn? 1f : -1f) / duration;

		int index = _tracks.IndexOf(layer);
		if (index < 0) {
			Debug.LogError(string.Format("Layer {0} not found.", layer.name));
			yield break;
		}
		float targetVolume = _trackVolumes[index] * _volume;
		if (fadeIn) {
			layer.volume = 0;
			layer.Play();
		}
		
		var wait = new WaitForEndOfFrame();
		float lastTime = ignoreTimeScale? Time.realtimeSinceStartup : Time.timeSinceLevelLoad;
		float currentTime;
		float deltaTime;
		
		while ((fadeIn && layer.volume < targetVolume) || (!fadeIn && layer.volume > 0f)) {
			currentTime = ignoreTimeScale? Time.realtimeSinceStartup : Time.timeSinceLevelLoad;
			deltaTime = currentTime - lastTime;
			
			layer.volume = Mathf.Clamp01(layer.volume + delta * deltaTime);
			
			lastTime = currentTime;
			
			yield return wait;
		}
		
		if (!fadeIn) {
			layer.Stop();
		}

		layer.volume = targetVolume;
	}

}

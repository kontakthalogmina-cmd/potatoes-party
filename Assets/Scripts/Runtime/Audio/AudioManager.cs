using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>Odtwarza muzykę (z płynnym przejściem) i efekty dźwiękowe.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        AudioSource musicA, musicB, sfx;
        Dictionary<Sfx, AudioClip> clips;
        readonly Dictionary<string, AudioClip> songs = new Dictionary<string, AudioClip>();
        string currentSong;
        Coroutine fade;

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var am = go.AddComponent<AudioManager>();
            Instance = am;
            am.musicA = am.NewSource(true);
            am.musicB = am.NewSource(true);
            am.sfx = am.NewSource(false);
            am.clips = Synth.MakeSfx();
            return am;
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        float MusicVol { get { return GameSettings.MusicVolume / 100f * 0.55f; } }

        public void ApplyVolumes()
        {
            if (musicA == null) return;
            if (fade == null)
            {
                if (musicA.isPlaying) musicA.volume = MusicVol;
                if (musicB.isPlaying) musicB.volume = MusicVol;
            }
            sfx.volume = GameSettings.SfxVolume / 100f;
        }

        AudioClip Song(Synth.SongStyle style)
        {
            AudioClip c;
            if (!songs.TryGetValue(style.Name, out c))
            {
                c = Synth.MakeSong(style);
                songs[style.Name] = c;
            }
            return c;
        }

        public void PlayMusic(Synth.SongStyle style)
        {
            if (currentSong == style.Name) return;
            currentSong = style.Name;
            var clip = Song(style);
            var from = musicA.isPlaying ? musicA : musicB;
            var to = from == musicA ? musicB : musicA;
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Crossfade(from, to));
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            float t = 0f;
            float startFrom = from.volume;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.8f;
                to.volume = Mathf.Lerp(0f, MusicVol, t);
                from.volume = Mathf.Lerp(startFrom, 0f, t);
                yield return null;
            }
            from.Stop();
            to.volume = MusicVol;
            fade = null;
        }

        float warp = 1f;

        /// <summary>
        /// Dźwięk podąża za czasem gry (TimeDirector): w slow-motion muzyka i efekty schodzą w dół
        /// (pitch = skala^0.35, więc 0.25× ≈ -0.4 oktawy, a nie "kaseta wciągnięta w magnetofon"),
        /// w hit-stopie (0.02) dźwięk trafienia gra normalnie – pitch nie spada poniżej 0.55.
        /// </summary>
        public void SetTimeWarp(float timeScale)
        {
            float w = Mathf.Clamp(Mathf.Pow(Mathf.Clamp01(timeScale), 0.35f), 0.55f, 1f);
            if (timeScale <= TimeDirector.HitStopScale + 0.001f) w = 1f; // hit-stop: krótki, nie zniekształcamy
            if (Mathf.Abs(w - warp) < 0.002f) return;
            warp = w;
            if (musicA != null) musicA.pitch = w;
            if (musicB != null) musicB.pitch = w;
        }

        public static void Play(Sfx s, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (Instance == null || Instance.clips == null) return;
            AudioClip c;
            if (!Instance.clips.TryGetValue(s, out c)) return;
            Instance.sfx.pitch = Instance.warp * (1f + Random.Range(-pitchJitter, pitchJitter));
            Instance.sfx.PlayOneShot(c, volume);
        }

        /// <summary>Dźwięk UI o zadanej wysokości (np. rosnąca gama przy najeżdżaniu na kolejne przyciski).
        /// Osobne źródło, żeby wysokość nie "przeciekała" na efekty rozgrywki.</summary>
        public static void PlayPitched(Sfx s, float volume, float pitch)
        {
            if (Instance == null || Instance.clips == null) return;
            AudioClip c;
            if (!Instance.clips.TryGetValue(s, out c)) return;
            if (Instance.ui == null)
            {
                Instance.ui = Instance.gameObject.AddComponent<AudioSource>();
                Instance.ui.playOnAwake = false;
                Instance.ui.spatialBlend = 0f;
                Instance.ui.outputAudioMixerGroup = Instance.sfx.outputAudioMixerGroup;
            }
            Instance.ui.volume = Instance.sfx.volume;
            Instance.ui.pitch = pitch;
            Instance.ui.PlayOneShot(c, volume);
        }

        AudioSource ui;
    }
}

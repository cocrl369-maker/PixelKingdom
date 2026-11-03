using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Spielt die im Code komponierte Musik (12 ruhige Stücke im Wechsel) und den Klick-Ton.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I;
        AudioSource music, sfx;
        AudioClip click;
        AudioClip currentClip, nextClip;
        Task<float[]> pending;
        int nextSeed;
        float pause = 2f;

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt("pk_music", 1) == 1;
            set { PlayerPrefs.SetInt("pk_music", value ? 1 : 0); if (!value && I != null) I.music.Stop(); }
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt("pk_sfx", 1) == 1;
            set => PlayerPrefs.SetInt("pk_sfx", value ? 1 : 0);
        }

        void Awake()
        {
            I = this;
            if (FindObjectOfType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            }
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false; music.loop = false; music.volume = 0.3f; music.spatialBlend = 0f;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.volume = 0.8f; sfx.spatialBlend = 0f;

            var c = MusicSynth.Click();
            click = AudioClip.Create("Klick", c.Length, 1, MusicSynth.SR, false);
            click.SetData(c, 0);

            // Stücke im Hintergrund komponieren – immer nur das nächste, damit wenig Speicher belegt ist
            nextSeed = Random.Range(0, MusicSynth.PieceCount);
            Compose();
        }

        void Compose()
        {
            int seed = nextSeed + 1;
            nextSeed = (nextSeed + 1 + Random.Range(0, 2)) % MusicSynth.PieceCount;
            pending = Task.Run(() => MusicSynth.Piece(seed));
        }

        void Update()
        {
            if (pending != null && pending.IsCompleted)
            {
                if (!pending.IsFaulted && pending.Result != null)
                {
                    nextClip = AudioClip.Create("Musik", pending.Result.Length, 1, MusicSynth.SR, false);
                    nextClip.SetData(pending.Result, 0);
                }
                pending = null;
            }
            if (!MusicOn || music.isPlaying) return;
            if (nextClip == null) return;
            pause -= Time.unscaledDeltaTime;
            if (pause > 0) return;
            if (currentClip != null) { music.clip = null; Destroy(currentClip); }
            currentClip = nextClip; nextClip = null;
            music.clip = currentClip;
            music.Play();
            Compose();                                   // das folgende Stück schon vorbereiten
            pause = 6f + Random.value * 8f;              // längere Stille zwischen den Stücken
        }

        public static void Click()
        {
            if (I == null || !SoundOn || I.click == null) return;
            I.sfx.pitch = 0.95f + Random.value * 0.1f;
            I.sfx.PlayOneShot(I.click, 0.75f);
        }
    }
}

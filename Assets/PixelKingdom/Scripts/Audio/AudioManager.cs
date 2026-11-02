using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Spielt die im Code komponierte Musik (mehrere Stücke im Wechsel) und den Klick-Ton.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I;
        AudioSource music, sfx;
        AudioClip click;
        readonly List<AudioClip> pieces = new List<AudioClip>();
        readonly List<Task<float[]>> tasks = new List<Task<float[]>>();
        int current = -1;
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
            music.playOnAwake = false; music.loop = false; music.volume = 0.38f; music.spatialBlend = 0f;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.volume = 0.8f; sfx.spatialBlend = 0f;

            var c = MusicSynth.Click();
            click = AudioClip.Create("Klick", c.Length, 1, MusicSynth.SR, false);
            click.SetData(c, 0);

            // Stücke im Hintergrund komponieren (dauert einen Moment, blockiert nichts)
            for (int k = 0; k < 4; k++)
            {
                int seed = k + 1;
                tasks.Add(Task.Run(() => MusicSynth.Piece(seed)));
            }
        }

        void Update()
        {
            for (int k = tasks.Count - 1; k >= 0; k--)
            {
                var t = tasks[k];
                if (!t.IsCompleted) continue;
                tasks.RemoveAt(k);
                if (t.IsFaulted || t.Result == null) continue;
                var clip = AudioClip.Create("Musik " + (pieces.Count + 1), t.Result.Length, 1, MusicSynth.SR, false);
                clip.SetData(t.Result, 0);
                pieces.Add(clip);
            }
            if (!MusicOn || pieces.Count == 0) return;
            if (music.isPlaying) return;
            pause -= Time.unscaledDeltaTime;
            if (pause > 0) return;
            current = (current + 1) % pieces.Count;
            music.clip = pieces[current];
            music.Play();
            pause = 4f + Random.value * 5f; // kurze Stille zwischen den Stücken
        }

        public static void Click()
        {
            if (I == null || !SoundOn || I.click == null) return;
            I.sfx.pitch = 0.95f + Random.value * 0.1f;
            I.sfx.PlayOneShot(I.click, 0.75f);
        }
    }
}

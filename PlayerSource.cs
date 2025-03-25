using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OuterVoice
{
    internal class PlayerSource : MonoBehaviour
    {
        private AudioSource[] srcs;
        private int idx;
        private bool running;

        private double currentTime;
        private double nextTime;
        private double clipTime;

        public bool isInitialized = false;

        Queue<AudioClip> clips;

        public void Initialize(GameObject player, double clipTimeIn)
        {
            running = false;
            idx = 0;

            srcs = new AudioSource[2];
            srcs[0] = CreateSource(player);
            srcs[1] = CreateSource(player);

            clips = new Queue<AudioClip>();
            clipTime = clipTimeIn;

            isInitialized = true;
        }

        public void AddToQueue(AudioClip clip)
        {
            clips.Enqueue(clip);
        }

        private AudioSource CreateSource(GameObject player)
        {
            AudioSource source = player.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.volume = 1f;
            source.maxDistance = 100f;

            return source;
        }

		public void Cleanup()
		{
			foreach (var clip in clips)
			{
				if (clip != null)
				{
					Destroy(clip);
				}
			}

			foreach (var src in srcs)
			{
				if (src != null)
				{
					Destroy(src);
				}
			}
		}

		public void Play()
        {
            if (!running)
            {
                if (clips.Count > 1)
                {
                    srcs[idx].clip = clips.Dequeue();
                    srcs[idx].PlayScheduled(AudioSettings.dspTime);
                    StartCoroutine(FadeClip(srcs[idx], AudioSettings.dspTime));

                    float clipLength = (float)srcs[idx].clip.samples / srcs[idx].clip.frequency;
                    nextTime = AudioSettings.dspTime + clipLength;

                    idx = (idx == 0) ? 1 : 0;
                    running = true;
                }
            }
            else
            {
                currentTime = AudioSettings.dspTime;
                if (currentTime + 0.02f > nextTime && clips.Count > 0)
                {
                    srcs[idx].clip = clips.Dequeue();
                    srcs[idx].PlayScheduled(nextTime);
                    StartCoroutine(FadeClip(srcs[idx], nextTime));

                    float clipLength = (float)srcs[idx].clip.samples / srcs[idx].clip.frequency;
                    nextTime = nextTime + clipLength;

                    idx = (idx == 0) ? 1 : 0;
                }
                if (clips.Count < 1 && currentTime > nextTime)
                {
                    running = false;
                }
            }
        }
        private IEnumerator FadeClip(AudioSource source, double nextTime)
        {
            float startVolume = source.volume;
            source.volume = 0;

            double delay = nextTime - AudioSettings.dspTime;
            yield return new WaitForSeconds((float)delay);

            float fadeDuration = 0.1f;

            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                source.volume = Mathf.Lerp(0, startVolume, t / fadeDuration);
                yield return null;
            }

            while (source.time / source.clip.length < 0.9f) yield return null;

            fadeDuration = source.clip.length * 0.1f;

            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                source.volume = Mathf.Lerp(startVolume, 0, t / fadeDuration);
                yield return null;
            }

            source.volume = 0;
            Destroy(source.clip);
            source.volume = startVolume;
        }
    }
}

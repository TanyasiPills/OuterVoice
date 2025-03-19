using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OuterVoice
{
    internal class PlayerSource
    {
        private AudioSource[] srcs;
        private int idx;
        private bool running;

        private double currentTime;
        private double nextTime;
        private double clipTime;

        Queue<AudioClip> clips;

        public PlayerSource(GameObject player, double clipTimeIn)
        {
            running = false;
            idx = 0;

            srcs = new AudioSource[2];
            srcs[0] = CreateSource(player);
            srcs[1] = CreateSource(player);

            clips = new Queue<AudioClip>();
            clipTime = clipTimeIn;
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

        public void Play()
        {
            if (!running)
            {
                if (clips.Count > 0)
                {
                    currentTime = AudioSettings.dspTime;
                    nextTime = currentTime + clipTime;

                    srcs[idx].clip = clips.Dequeue();
                    srcs[idx].PlayScheduled(nextTime);

                    nextTime = nextTime + clipTime;

                    idx = (idx == 0) ? 1 : 0;
                    running = true;
                }
            }
            else
            {
                currentTime = AudioSettings.dspTime;
                if (currentTime + 0.05f > nextTime && clips.Count > 0)
                {
                    srcs[idx].clip = clips.Dequeue();
                    srcs[idx].PlayScheduled(nextTime);

                    nextTime = nextTime + clipTime;
                    idx = (idx == 0) ? 1 : 0;
                }
                if (clips.Count < 1)
                {
                    running = false;
                }
            }
        }
    }
}

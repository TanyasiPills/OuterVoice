using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OuterVoice
{
    public class OuterVoice : ModBehaviour
    {
        public static OuterVoice Instance;
        public static IQSBAPI buddyApi;

        private AudioClip audioClip;

        private float[] data;
        private int freq = 8192*2;
        private int chunkSize = 4096;
        private int sendSize = 8192 / 10;
        private AudioClip clip;
        [SerializeField] private string mic;

        Dictionary<uint, AudioSource> audioSources;

        uint myId = 999;

        private Dictionary<uint, Queue<float>> voiceBuffers = new Dictionary<uint, Queue<float>>();
        private Queue<AudioClip> toPlay = new Queue<AudioClip>();

        public void Awake(){Instance = this;}

        public void Start()
        {
            audioSources = new Dictionary<uint, AudioSource>();
            buddyApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            ModHelper.Console.WriteLine($"Swompy mompy, {nameof(OuterVoice)} is loaded!", MessageType.Success);

            new Harmony("Maychii.OuterVoice").PatchAll(Assembly.GetExecutingAssembly());

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            mic = Microphone.devices[0];
        }

        private IEnumerator Record()
        {
            clip = Microphone.Start(mic, true, 1, freq);
            data = new float[freq];

            while (Microphone.GetPosition(mic) < 0) yield return null;

            int lastPos = 0;

            while (true)
            {
                yield return new WaitForSeconds(0.05f);
                int pos = Microphone.GetPosition(mic);
                if (pos >= sendSize && pos != lastPos)
                {
                    clip.GetData(data, pos - sendSize);

                    float[] chunkData = new float[sendSize];
                    Array.Copy(data, chunkData, sendSize);

                    SendVoice(chunkData);

                    lastPos = pos;
                }
            }

        }

        private void SendVoice(float[] data)
        {
            if (data != null)
            {
                buddyApi.SendMessage("voice", data);
            }
            else
            {
                ModHelper.Console.WriteLine("Data is null, cannot send voice!", MessageType.Error);
            }
        }

        private void GetVoice(uint sender, float[] data)
        {
            if (!voiceBuffers.ContainsKey(sender))
                voiceBuffers[sender] = new Queue<float>();


            voiceBuffers[sender] = new Queue<float>(voiceBuffers[sender].Concat(data));

            if (!audioSources.ContainsKey(sender))
            {
                ModHelper.Console.WriteLine($"Audio source for player {sender} is not initialized!", MessageType.Error);
                return;
            }

            AudioSource source = audioSources[sender];

            if(voiceBuffers[sender].Count > chunkSize)
            {
                float[] toVoice = voiceBuffers[sender].Take(chunkSize).ToArray();
                voiceBuffers[sender] = new Queue<float>(voiceBuffers[sender].Skip(chunkSize));
                AudioClip clipToPlay = AudioClip.Create("clipike", toVoice.Length, 1, freq, false);
                clipToPlay.SetData(toVoice, 0);
                toPlay.Enqueue(clipToPlay);
            }

            if (!source.isPlaying && toPlay.Count > 0)
            {
                AudioClip clipIn = toPlay.Dequeue();
                source.clip = clipIn;
                source.Play();
            }
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;

            ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
            if (buddyApi.GetIsHost())
            {
                myId = 1;
                ModHelper.Console.WriteLine($"joined as host", MessageType.Success);
            }
            else StartCoroutine(WaitForLocalPlayerInitialization());


            buddyApi.OnPlayerJoin().AddListener(PlayerJoined);

            ModHelper.Console.WriteLine($"yam", MessageType.Success);
            uint[] ids = buddyApi.GetPlayerIDs();
            foreach (uint id in ids)
            {
                if (id != myId) StartCoroutine(WaitForPlayerAndSetupAudio(id));
            }
            if (buddyApi.GetIsHost())
            {
                StartCoroutine(Record());
                buddyApi.RegisterHandler<float[]>("voice", GetVoice);
            }
        }

        private IEnumerator WaitForLocalPlayerInitialization()
        {
            while (buddyApi.GetLocalPlayerID() == 0)
            {
                yield return null;
            }

            myId = buddyApi.GetLocalPlayerID();
            ModHelper.Console.WriteLine($"Local player initialized with ID: {myId}", MessageType.Success);

            while (!buddyApi.GetPlayerReady(myId))
            {
                ModHelper.Console.WriteLine($"Waiting for myself to be ready...", MessageType.Info);
                yield return new WaitForSeconds(0.5f);
            }

            ModHelper.Console.WriteLine($"joined as: {myId}");
            StartCoroutine(Record());
            buddyApi.RegisterHandler<float[]>("voice", GetVoice);
        }

        private void PlayerJoined(uint playerID)
        {
            if (playerID == 1) return;
            StartCoroutine(WaitForPlayerAndSetupAudio(playerID));
            ModHelper.Console.WriteLine($"Player id: {playerID}");
        }
        
        private IEnumerator WaitForPlayerAndSetupAudio(uint id)
        {
            while (!buddyApi.GetPlayerReady(id))
            {
                ModHelper.Console.WriteLine($"Waiting for player[{id}] to be ready...", MessageType.Info);
                yield return new WaitForSeconds(0.5f);
            }
            GameObject player = buddyApi.GetPlayerBody(id);
            if (player == null)
            {
                ModHelper.Console.WriteLine("Player not found!", MessageType.Error);
                yield break;
            }

            AudioSource souce = player.GetComponent<AudioSource>();
            if (souce == null)
            {
                souce = player.AddComponent<AudioSource>();
            }

            if (audioSources == null)
            {
                audioSources = new Dictionary<uint, AudioSource>();
            }

            souce.playOnAwake = false;
            souce.loop = false;
            souce.spatialBlend = 1f;
            souce.volume = 1f;
            souce.maxDistance = 100f;

            audioSources[id] = souce;
        }      
    }
}

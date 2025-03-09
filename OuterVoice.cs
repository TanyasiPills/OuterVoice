using System;
using System.Collections;
using System.Collections.Generic;
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

        private bool playerInGame = false;

        private float[] data;
        private int freq = 44100;
        private AudioClip clip;
        [SerializeField] private string mic;

        Dictionary<uint, AudioSource> audioSources;

        uint myId = 999;

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
            clip = Microphone.Start(mic, true, 999, freq);
            data = new float[freq];

            while (Microphone.GetPosition(mic) < 0) yield return null;

            int lastPos = 0;

            while (true)
            {
                yield return new WaitForSeconds(0.008333f);
                int pos = Microphone.GetPosition(mic);
                if (pos < freq || pos == lastPos) continue;
                clip.GetData(data, pos - freq);

                SendVoice(data);

                lastPos = pos;
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
            if (audioSources.ContainsKey(sender))
            {
                AudioSource source = audioSources[sender];
                if (source.clip == null)
                {
                    AudioClip clipToPlay = AudioClip.Create("clipike", data.Length, 1, 44100, false);
                    clipToPlay.SetData(data, 0);
                    audioSources[sender].clip = clipToPlay;
                }
                else
                {
                    int currentPos = source.timeSamples;
                    float[] newClipData = new float[source.clip.samples + data.Length];
                    source.clip.GetData(newClipData, 0);
                    Array.Copy(data, 0, newClipData, currentPos, data.Length);
                    source.clip.SetData(newClipData, 0);
                }
                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
            else
            {
                ModHelper.Console.WriteLine($"Audio source for player {sender} is not initialized!", MessageType.Error);
            }

        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;

            ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
            buddyApi.OnPlayerJoin().AddListener(PlayerJoined);

            myId = buddyApi.GetLocalPlayerID();

            ModHelper.Console.WriteLine($"yam", MessageType.Success);
            uint[] ids = buddyApi.GetPlayerIDs();
            foreach (uint id in ids)
            {
                if (id != myId) StartCoroutine(WaitForPlayerAndSetupAudio(id));
            }
            ModHelper.Console.WriteLine($"joined as: {myId}");
            buddyApi.GetPlayerReady(myId);
            StartCoroutine(Record());
            buddyApi.RegisterHandler<float[]>("voice", GetVoice);
        }

        private void PlayerJoined(uint playerID)
        {
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

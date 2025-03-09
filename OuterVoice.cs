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
        private Camera camera;

        private bool playerInGame = false;

        private float[] data;
        private int freq = 44100;
        private AudioClip clip;
        [SerializeField] private string mic;

        Dictionary<uint, AudioSource> audioSources;

        uint myId = 999;

        bool gotId = false;

        public void Awake(){Instance = this;}

        public void Start()
        {
            buddyApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            ModHelper.Console.WriteLine($"Swompy mompy, {nameof(OuterVoice)} is loaded!", MessageType.Success);

            new Harmony("Maychii.OuterVoice").PatchAll(Assembly.GetExecutingAssembly());

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            buddyApi.RegisterHandler<AudioClip>("voice", GetVoice);

            mic = Microphone.devices[0];
            StartCoroutine(Record());
        }

        private IEnumerator Record()
        {
            clip = Microphone.Start(mic, true, 999, freq);
            data = new float[freq];

            while (Microphone.GetPosition(mic) < 0) yield return null;

            while (true)
            {
                yield return new WaitForSeconds(1);
                int pos = Microphone.GetPosition(mic);
                if (pos < freq) continue;
                clip.GetData(data, pos - freq);

                AudioClip clipToSend = AudioClip.Create("clipike", data.Length, 1, 44100, false);
                clipToSend.SetData(data, 0);
                SendVoice(clipToSend);
            }

        }

        private void SendVoice(AudioClip clip)
        {
            buddyApi.SendMessage("voice", clip);
            ModHelper.Console.WriteLine($"Voice sent");
        }

        private void GetVoice(uint sender, AudioClip clip)
        {
            audioSources[sender].clip = clip;
            audioSources[sender].Play();
            ModHelper.Console.WriteLine($"Playing voice from player {sender}");
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;

            camera = Camera.main;

            ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
            buddyApi.OnPlayerJoin().AddListener(PlayerJoined);
            
        }

        private void PlayerJoined(uint playerID)
        {
            if (buddyApi.GetLocalPlayerID() == playerID)
            {
                myId = playerID;
                gotId = true;
                uint[] ids = buddyApi.GetPlayerIDs();
                foreach (uint id in ids)
                {
                    if(id != playerID) StartCoroutine(WaitForPlayerAndSetupAudio(playerID));
                }
            }
            else
            {
                StartCoroutine(WaitForPlayerAndSetupAudio(playerID));
            }
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

            souce.playOnAwake = false;
            souce.loop = false;
            souce.spatialBlend = 1f;
            souce.volume = 1f;
            souce.maxDistance = 100f;

            audioSources[id] = souce;
        }      
    }
}

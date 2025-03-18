using System;
using System.Threading;
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
        private float voiceVolume;
        private float lastVoiceVolume = 1.0f;


        private int freq = 32768;
        private int chunkSize = 8192;
        private int sendSize = 8192;
        private AudioClip clip;
        [SerializeField] private string mic;

        AudioSource myVoice;
        Queue<float> myQueue = new Queue<float>();
        private Queue<AudioClip> myToPlay = new Queue<AudioClip>();

        Camera camera;

        Dictionary<uint, AudioSource> audioSources;

        uint myId = 999;

        private Dictionary<uint, Queue<float>> voiceBuffers = new Dictionary<uint, Queue<float>>();
        private Queue<AudioClip> toPlay = new Queue<AudioClip>();

        bool running = false;

        public void Awake(){Instance = this;}

        public void Start()
        {
            voiceVolume = ModHelper.Config.GetSettingsValue<int>("Voice Volume")/100;
            audioSources = new Dictionary<uint, AudioSource>();
            buddyApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            ModHelper.Console.WriteLine($"Swompy mompy, {nameof(OuterVoice)} is loaded!", MessageType.Success);

            new Harmony("Maychii.OuterVoice").PatchAll(Assembly.GetExecutingAssembly());

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            string micName = "PnP";
            mic = Microphone.devices.FirstOrDefault(d => d.Contains(micName));
            ModHelper.Console.WriteLine($"Microphone: {mic}", MessageType.Success);
        }

        private IEnumerator Record()
        {

            clip = Microphone.Start(mic, false, 999, freq);

            while (Microphone.GetPosition(mic) < 0) yield return null;

            int lastPos = 0;

            while (true)
            {
                yield return new WaitForSeconds(0.05f);
                int pos = Microphone.GetPosition(mic);
                if (pos != lastPos)
                {
                    int length = pos - lastPos;

                    float[] data = new float[length];
                    clip.GetData(data, lastPos);
                    if (data.Max() > 0.05f)
                    {
                        SendVoice(data);
                        PutItInsideMe(data);
                    }

                    lastPos = pos;
                }
            }

        }

        private void PutItInsideMe(float[] data)
        {
            if (myVoice != null)
            {
                myQueue = new Queue<float>(myQueue.Concat(data));   
            }
        }

        private void PlayMe()
        {
            if (myQueue.Count > chunkSize)
            {
                float[] toVoice = myQueue.Take(chunkSize).ToArray();
                myQueue = new Queue<float>(myQueue.Skip(chunkSize));
                AudioClip clipToPlay = AudioClip.Create("clipike", toVoice.Length, 1, freq, false);
                clipToPlay.SetData(toVoice, 0);
                myToPlay.Enqueue(clipToPlay);
            }

            if (!myVoice.isPlaying && myToPlay.Count > 0)
            {
                AudioClip clipIn = myToPlay.Dequeue();
                myVoice.clip = clipIn;
                myVoice.Play();
            }
        }

        private void Update()
        {
            if (myVoice != null) PlayMe();

            float newVolume = ModHelper.Config.GetSettingsValue<int>("Voice Volume") / 100;

            if (newVolume != lastVoiceVolume)
            {
                lastVoiceVolume = newVolume;
                voiceVolume = newVolume;
                ApplyVolumeToAllSources();
            }

            if (Keyboard.current[Key.J].wasPressedThisFrame)
            {
                RaycastFromCamera();
            }
        }


        private void SpawnAudioPlayer(Vector3 positionIn, Transform parentIn)
        {
            GameObject audioplayer = new GameObject("AudioPlayer");
            audioplayer.transform.SetParent(parentIn);
            audioplayer.transform.position = positionIn;
            myVoice = audioplayer.AddComponent<AudioSource>();
            myVoice.playOnAwake = false;
            myVoice.loop = false;
            myVoice.spatialBlend = 1f;
            myVoice.volume = 10f;
            myVoice.maxDistance = 100f;
        }

        private void RaycastFromCamera()
        {
            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f))
            {
                ModHelper.Console.WriteLine("collision detected at position: " + hit.point.ToString());
                Transform parent = hit.transform;
                SpawnAudioPlayer(hit.point, parent);
            }
        }

        private void ApplyVolumeToAllSources()
        {
            ModHelper.Console.WriteLine($"Audio volume changed!", MessageType.Info);
            foreach (var source in audioSources.Values)
            {
                source.volume = voiceVolume;
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

            camera = Camera.main;

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

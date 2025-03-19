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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Epic.OnlineServices;

namespace OuterVoice
{
    public class OuterVoice : ModBehaviour
    {
        public static OuterVoice Instance;
        public static IQSBAPI buddyApi;

        private AudioClip audioClip;
        private float voiceVolume;
        private float micVolume;
        private float lastVoiceVolume = 0;
        private float lastMicVolume = 0;


        private int freq = 44100;
        private int chunkSize = 22050;
        private double clipTime;
        private double currentTime;
        private double nextTime;
        private AudioClip clip;
        [SerializeField] private string mic;

        AudioSource myVoice;
        AudioSource myVoice2;
        AudioSource[] myVoices = new AudioSource[2];
        bool[] hasClip = {false, false};
        List<float> myQueue = new List<float>();
        private Queue<AudioClip> myToPlay = new Queue<AudioClip>();

        Camera camera;

        Dictionary<uint, AudioSource> audioSources;

        uint myId = 999;

        private Dictionary<uint, List<float>> voiceBuffers = new Dictionary<uint, List<float>>();
        private Dictionary<uint, Queue<AudioClip>> toPlay = new Dictionary<uint, Queue<AudioClip>>();

        bool running = false;
        int srcNow = 0;

        public void Awake() { 
            Instance = this;
        }

        public override void Configure(IModConfig config)
        {
            if (ModHelper.Config == null)
            {
                ModHelper.Console.WriteLine("ModHelper.Config is not initialized!", MessageType.Error);
                return;
            }

            if (config == null)
            {
                ModHelper.Console.WriteLine("Config is null!", MessageType.Error);
                return;
            }

            var ja = config.GetSettingsValue<string>("Used Mic");

            if (string.IsNullOrEmpty(ja))
            {
                ModHelper.Console.WriteLine("No mic selection in config!", MessageType.Info);
                return;
            }

            string micSelected = ja.Replace(".", "");
            ModHelper.Console.WriteLine($"String: {ja}", MessageType.Info);

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                ModHelper.Console.WriteLine("No microphone devices found!", MessageType.Error);
                return;
            }

            var matchingMic = Microphone.devices.FirstOrDefault(e => e.Contains(micSelected));

            if (matchingMic != null)
            {
                mic = matchingMic;
                ModHelper.Console.WriteLine($"Value: {mic}", MessageType.Success);
            }
            else
            {
                ModHelper.Console.WriteLine("Selected microphone not found in the list!", MessageType.Error);
            }
        }

        public void Start()
        {
            IModConfig config = ModHelper.Config;
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict["type"] = "selector";
            List<string> list = Microphone.devices.Select(e => e.Substring(0, 14) + "...").ToList();
            dict["options"] = list;

            JObject newSettings = JObject.FromObject(dict);
            config.Settings["Used Mic"] = newSettings;

            clipTime = chunkSize / freq;
            audioSources = new Dictionary<uint, AudioSource>();
            buddyApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            ModHelper.Console.WriteLine($"Swompy mompy, {nameof(OuterVoice)} is loaded!", MessageType.Success);

            new Harmony("Maychii.OuterVoice").PatchAll(Assembly.GetExecutingAssembly());

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        private IEnumerator Record()
        {

            clip = Microphone.Start(mic, false, 999, freq);

            while (Microphone.GetPosition(mic) < 0) yield return null;

            int lastPos = 0;

            while (true)
            {
                yield return null;
                int pos = Microphone.GetPosition(mic);
                if (pos != lastPos)
                {
                    int length = pos - lastPos;

                    float[] data = new float[length];
                    clip.GetData(data, lastPos);

                    Parallel.For(0, data.Length, i =>
                    {
                        data[i] = Mathf.Clamp(data[i] * micVolume, -1.0f, 1.0f);
                    });

                    if (data.Max() > 0.005f)
                    {
                        SendVoice(data);
                        RelaySelf(data);
                    }

                    lastPos = pos;
                }
            }

        }

        private void RelaySelf(float[] data)
        {
            if (myVoice != null)
            {
                myQueue.AddRange(data);

                if (myQueue.Count > chunkSize)
                {
                    float[] toVoice = myQueue.Take(chunkSize).ToArray();
                    myQueue.RemoveRange(0, chunkSize);
                    
                    Parallel.For(0, toVoice.Length, i =>
                    {
                        toVoice[i] = Mathf.Clamp(toVoice[i] * voiceVolume, -1.0f, 1.0f);
                    });
                    
                    AudioClip clipToPlay = AudioClip.Create("clipike", toVoice.Length, 1, freq, false);
                    clipToPlay.SetData(toVoice, 0);
                    myToPlay.Enqueue(clipToPlay);
                }

            }
        }

        private void PlaySelf()
        {
            if (!running)
            {
                if(myToPlay.Count > 0)
                {
                    myVoices[srcNow].clip = myToPlay.Dequeue();
                    currentTime = AudioSettings.dspTime;
                    nextTime = currentTime + clipTime;
                    myVoices[srcNow].PlayScheduled(nextTime);
                    nextTime = nextTime + clipTime;
                    srcNow = (srcNow == 0) ? 1 : 0;
                    running = true;
                }
            }
            else
            {
                currentTime = AudioSettings.dspTime;
                if(currentTime + 0.05f > nextTime && myToPlay.Count > 0)
                {
                    myVoices[srcNow].clip = myToPlay.Dequeue();
                    myVoices[srcNow].PlayScheduled(nextTime);
                    nextTime = nextTime + clipTime;
                    srcNow = (srcNow == 0) ? 1 : 0;
                }
                if (myToPlay.Count < 1)
                {
                    running = false;
                }
            }
        }

        private void Update()
        {
            if (myVoice != null) PlaySelf();

            float newVolume = ModHelper.Config.GetSettingsValue<float>("Voice Volume");
            IModConfig config;

            if (newVolume != lastVoiceVolume)
            {
                ModHelper.Console.WriteLine($"Audio volume(real): {newVolume}", MessageType.Info);
                lastVoiceVolume = newVolume;
                voiceVolume = newVolume / 500;
                ModHelper.Console.WriteLine($"Audio volume: {voiceVolume}", MessageType.Info);
                //ApplyVolumeToAllSources();
            }

            float newMicVolume = ModHelper.Config.GetSettingsValue<float>("Mic Volume");

            if (newMicVolume != lastVoiceVolume)
            {
                lastMicVolume = newMicVolume;
                micVolume = newMicVolume / 500;
                //ApplyVolumeToAllSources();
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
            myVoice.volume = 1f;
            myVoice.maxDistance = 100f;

            myVoice2 = audioplayer.AddComponent<AudioSource>();
            myVoice2.playOnAwake = false;
            myVoice2.loop = false;
            myVoice2.spatialBlend = 1f;
            myVoice2.volume = 1f;
            myVoice2.maxDistance = 100f;

            myVoices[0] = myVoice;
            myVoices[1] = myVoice2;
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
            if (myVoices[0] != null)
            {
                myVoices[0].volume = voiceVolume;
                myVoices[1].volume = voiceVolume;
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
        /*
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
        */
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
                //buddyApi.RegisterHandler<float[]>("voice", GetVoice);
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
            //buddyApi.RegisterHandler<float[]>("voice", GetVoice);
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

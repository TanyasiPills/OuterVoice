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

namespace OuterVoice
{
    public class OuterVoice : ModBehaviour
    {
        public static OuterVoice Instance;
        public static IQSBAPI buddyApi;

        private float voiceVolume;
        private float micVolume;
        private float lastVoiceVolume = 0;
        private float lastMicVolume = 0;


        private int freq = 44000;
        private int chunkSize = 22000;
        private double clipTime;
        private AudioClip clip;
        private string lastMic;
        private string mic;

        PlayerSource myPlayer;
        List<float> myQueue = new List<float>();

        Camera camera;

        uint myId = 999;

        private bool micSet = false;
        private bool ready = false;

        private Dictionary<uint, List<float>> voiceBuffers = new Dictionary<uint, List<float>>();
        private Dictionary<uint, PlayerSource> players = new Dictionary<uint, PlayerSource>();  

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

            //audio input
            var ja = config.GetSettingsValue<string>("Used Mic");

            if (string.IsNullOrEmpty(ja))
            {
                ModHelper.Console.WriteLine("No mic selected in config!", MessageType.Info);
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
                if(mic != lastMic) micSet = false;
                lastMic = mic;
            }
            else
            {
                ModHelper.Console.WriteLine("Selected microphone not found in the list!", MessageType.Error);
            }


            //volume
            float newVolume = ModHelper.Config.GetSettingsValue<float>("Voice Volume");

            if (newVolume != lastVoiceVolume)
            {
                ModHelper.Console.WriteLine($"Audio volume(real): {newVolume}", MessageType.Info);
                lastVoiceVolume = newVolume;
                voiceVolume = newVolume / 500;
                ModHelper.Console.WriteLine($"Audio volume: {voiceVolume}", MessageType.Info);
            }

            float newMicVolume = ModHelper.Config.GetSettingsValue<float>("Mic Volume");

            if (newMicVolume != lastMicVolume)
            {
                lastMicVolume = newMicVolume;
                micVolume = newMicVolume / 500;
            }
        }

        public void Start()
        {
            IModConfig config = ModHelper.Config;
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict["type"] = "selector";
            dict["value"] = Microphone.devices[0].Substring(0, 14) + "...";
            List<string> list = Microphone.devices.Select(e => e.Substring(0, 14) + "...").ToList();
            dict["options"] = list;

            JObject newSettings = JObject.FromObject(dict);
            config.Settings["Used Mic"] = newSettings;

            Configure(config);

            clipTime = chunkSize / freq;
            buddyApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            ModHelper.Console.WriteLine($"Swompy mompy, {nameof(OuterVoice)} is loaded!", MessageType.Success);
            
            new Harmony("Maychii.OuterVoice").PatchAll(Assembly.GetExecutingAssembly());
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        private IEnumerator Record()
        {
            ModHelper.Console.WriteLine("Started recoding");
            clip = Microphone.Start(mic, false, 999, freq);
            micSet = true;

            while (Microphone.GetPosition(mic) < 0) yield return null;

            int lastPos = 0;

            while (micSet)
            {
                yield return null;
                int pos = Microphone.GetPosition(mic);

                if (pos < lastPos) lastPos = 0;

                if (pos != lastPos && pos - lastPos >= 8800)
                {
					int length = pos - lastPos;

					float[] data = new float[length];
					clip.GetData(data, lastPos);

					if (data.Max() > 0.01f)
					{
						Parallel.For(0, data.Length, i =>
						{
							data[i] = Mathf.Clamp(data[i] * micVolume, -1.0f, 1.0f);
						});
						SendVoice(data);
						RelaySelf(data);
					}

					lastPos = pos;
                }
            }

        }

        private void GetVoice(uint sender, float[] data)
        {
            if (!voiceBuffers.ContainsKey(sender))
                voiceBuffers[sender] = new List<float>();

            voiceBuffers[sender].AddRange(data);

            if (voiceBuffers[sender].Count > chunkSize)
            {
                float[] toVoice = voiceBuffers[sender].Take(chunkSize).ToArray();
                voiceBuffers[sender].RemoveRange(0, chunkSize);

                Parallel.For(0, toVoice.Length, i => toVoice[i] = Mathf.Clamp(toVoice[i] * voiceVolume, -1.0f, 1.0f));

                AudioClip clipToPlay = AudioClip.Create("clipike", toVoice.Length, 1, freq, false);
                clipToPlay.SetData(toVoice, 0);
                players[sender].AddToQueue(clipToPlay);
            }
        }

        //debug purposes + fun
        private void RelaySelf(float[] data)
        {
            if (myPlayer != null)
            {
                myQueue.AddRange(data);
                while (myQueue.Count > 0)
                {
                    float[] toVoice;
                    int size = myQueue.Count % chunkSize;
                    if(size == 0) size = chunkSize;
                    toVoice = myQueue.Take(size).ToArray();
                    myQueue.RemoveRange(0, size);

                    Parallel.For(0, toVoice.Length, i => toVoice[i] = Mathf.Clamp(toVoice[i] * voiceVolume, -1.0f, 1.0f));

                    AudioClip clipToPlay = AudioClip.Create("clipike", toVoice.Length, 1, freq, false);
                    clipToPlay.SetData(toVoice, 0);
                    myPlayer.AddToQueue(clipToPlay);
                }
            }
        }

        private void PlayVoices()
        {
            foreach (var item in players)
            {
                item.Value.Play();
            }
        }

        private void Update()
        {
            if (ready)
            {
                if (myPlayer != null)
                {
                    myPlayer.Play();
                }
                PlayVoices();
                if(!micSet) StartCoroutine(Record());
            }

            if (Keyboard.current[Key.J].wasPressedThisFrame)
            {
                RaycastFromCamera();
            }
        }


        private void SpawnAudioPlayer(Vector3 positionIn, Transform parentIn)
        {
            GameObject audioplayer = new GameObject("AudioPlayer");
            myPlayer = audioplayer.AddComponent<PlayerSource>();
            myPlayer.Initialize(audioplayer, clipTime);
            ModHelper.Console.WriteLine("spawen audiopayer", MessageType.Message);
            audioplayer.transform.parent = parentIn;
        }

        private void RaycastFromCamera()
        {
            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f))
            {
                Transform parent = hit.transform;
                SpawnAudioPlayer(hit.point, parent);
            }
        }

        private void SendVoice(float[] data)
        {
            if (data != null) buddyApi.SendMessage("voice", data);
            else ModHelper.Console.WriteLine("Data is null, cannot send voice!", MessageType.Error);
        }
        
        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;

            ready = true;

            camera = Camera.main;

            if (buddyApi.GetIsHost())
            {
                myId = 1;
                ModHelper.Console.WriteLine($"joined as host", MessageType.Success);
            }
            else StartCoroutine(WaitForLocalPlayerInitialization());


            buddyApi.OnPlayerJoin().AddListener(PlayerJoined);

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

		private void PlayerJoined(uint playerID)
		{
			if (playerID == 1) return;
			StartCoroutine(WaitForPlayerAndSetupAudio(playerID));
		}

		private void PlayerLeave(uint playerID)
		{
            if (playerID == 1)
            {
				foreach (var item in players.Values)
				{
					item.Cleanup();
				}
				players.Clear();

				foreach (var entry in voiceBuffers.Values)
				{
					entry.Clear();
				}
				voiceBuffers.Clear();
            }
            else
            {
				if (players.ContainsKey(playerID))
				{
					players[playerID].Cleanup();
					players.Remove(playerID);
				}

				if (voiceBuffers.ContainsKey(playerID))
				{
					voiceBuffers[playerID].Clear();
					voiceBuffers.Remove(playerID);
				}
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
        
        private IEnumerator WaitForPlayerAndSetupAudio(uint id)
        {
            while (!buddyApi.GetPlayerReady(id))
            {
                ModHelper.Console.WriteLine($"Waiting for player[{id}] to be ready...", MessageType.Info);
                yield return new WaitForSeconds(1f);
            }

            GameObject player = buddyApi.GetPlayerBody(id);

            if (player == null)
            {
                ModHelper.Console.WriteLine("Player not found!", MessageType.Error);
                yield break;
            }

            PlayerSource playerSource = player.AddComponent<PlayerSource>();
            playerSource.Initialize(player, clipTime);
            players[id] = playerSource;
        }      
    }
}

using System.Collections;
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

        public void Awake(){Instance = this;}

        public void Start()
        {
            buddyApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            ModHelper.Console.WriteLine($"Swompy mompy, {nameof(OuterVoice)} is loaded!", MessageType.Success);

            LoadAudio("Assets/audio/whisle.mp3");

            new Harmony("Maychii.OuterVoice").PatchAll(Assembly.GetExecutingAssembly());

            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        private void LoadAudio(string path)
        {
            audioClip = ModHelper.Assets.GetAudio(path);
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
            playerInGame = true;
            //StartCoroutine(WaitForPlayerAndSetupAudio(playerID));
        }

        private void Update()
        {
            if (playerInGame)
            {
                if (Keyboard.current[Key.J].wasPressedThisFrame)
                {
                    ModHelper.Console.WriteLine("SUCK MY DIIIIIICCCKKKK");
                    RaycastFromCamera();
                }
            }
        }

        private void RaycastFromCamera()
        {
            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit, 100f))
            {
                ModHelper.Console.WriteLine("collision detected at position: " + hit.point.ToString());
                Transform parent = hit.transform;
                SpawnAudioPlayer(hit.point, parent);
            }
        }

        private void SpawnAudioPlayer(Vector3 positionIn, Transform parentIn)
        {
            GameObject audioplayer = new GameObject("AudioPlayer");
            audioplayer.transform.SetParent(parentIn);
            audioplayer.transform.position = positionIn;
            AudioSource audioSource = audioplayer.AddComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;
            audioSource.volume = 0.5f;
            audioSource.maxDistance = 50f;



            audioSource.Play();
        }
        /*
        private IEnumerator WaitForPlayerAndSetupAudio(uint id)
        {
            while (!buddyApi.GetPlayerReady(id))
            {
                ModHelper.Console.WriteLine("Waiting for player to be ready...", MessageType.Info);
                yield return new WaitForSeconds(0.5f);
            }
            GameObject player = buddyApi.GetPlayerBody(id);
            if (player == null)
            {
                ModHelper.Console.WriteLine("Player not found!", MessageType.Error);
                yield break;
            }

            _audioSource = player.GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = player.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 1f;
            _audioSource.volume = 0.5f;

            
        }

        private void LoadAndPlayAudio(string path)
        {
            
            if (_customClip == null)
            {
                ModHelper.Console.WriteLine("Failed to load audio: " + path, MessageType.Error);
                return;
            }

            _audioSource.clip = _customClip;
            _audioSource.Play();

            ModHelper.Console.WriteLine("Playing custom sound!", MessageType.Success);
        }
        */
    }
}

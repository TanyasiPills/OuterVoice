using System.Collections;
using System.Reflection;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;

namespace OuterVoice
{
    public class OuterVoice : ModBehaviour
    {
        public static OuterVoice Instance;
        public static IQSBAPI buddyApi;

        private AudioSource _audioSource;
        private AudioClip _customClip;

        private bool playerInGame = false;

        public void Awake(){Instance = this;}

        public void Start()
        {
            buddyApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            // Starting here, you'll have access to OWML's mod helper.
            ModHelper.Console.WriteLine($"My mod {nameof(OuterVoice)} is loaded!", MessageType.Success);

            new Harmony("Maychii.OuterVoice").PatchAll(Assembly.GetExecutingAssembly());

            // Example of accessing game code.
            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;

            ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
            buddyApi.OnPlayerJoin().AddListener(AudioPlayer);
            
        }

        private void AudioPlayer(uint playerID)
        {
            playerInGame = true;
            StartCoroutine(WaitForPlayerAndSetupAudio(playerID));
        }

        private void Update()
        {
            if(playerInGame)
            {
                if (Input.GetKeyUp(KeyCode.J))
                {
                    SpawnAudioPlayer();
                }
            }
        }

        private void SpawnAudioPlayer()
        {

        }

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

            LoadAndPlayAudio("Assets/audio/whisle.mp3");
        }

        private void LoadAndPlayAudio(string path)
        {
            _customClip = ModHelper.Assets.GetAudio(path);
            if (_customClip == null)
            {
                ModHelper.Console.WriteLine("Failed to load audio: " + path, MessageType.Error);
                return;
            }

            _audioSource.clip = _customClip;
            _audioSource.Play();

            ModHelper.Console.WriteLine("Playing custom sound!", MessageType.Success);
        }
    }
}

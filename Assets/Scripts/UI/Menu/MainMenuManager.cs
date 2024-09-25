﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.Prompts;
using NSMB.Utils;
using Photon.Realtime;
using Quantum;
using System.Threading.Tasks;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu {
    public class MainMenuManager : Singleton<MainMenuManager> {

        //---Static Variables
        public static readonly int NicknameMin = 2, NicknameMax = 20;

        //---Public Variables
        public bool nonNetworkShutdown;
        public AudioSource sfx, music;
        public Toggle spectateToggle;
        public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
        public GameObject titleSelected, mainMenuSelected, lobbySelected, currentLobbySelected, creditsSelected, updateBoxSelected, ColorName;
        public int currentSkin;

        //---Serialized Fields
        [Header("Managers")]
        [SerializeField] public PlayerListHandler playerList;
        [SerializeField] public RoomListManager roomManager;
        [SerializeField] private ColorChooser colorManager;
        [SerializeField] public MainMenuChat chat;
        [SerializeField] public RoomSettingsCallbacks roomSettingsCallbacks;

        [Header("UI Elements")]
        [SerializeField] private GameObject title;
        [SerializeField] private GameObject bg, mainMenu, lobbyMenu, createLobbyPrompt, webglCreateLobbyPrompt, privateRoomIdPrompt, inLobbyMenu, creditsMenu, updateBox;
        [SerializeField] private GameObject sliderText, currentMaxPlayers, settingsPanel;
        [SerializeField] private TMP_Dropdown levelDropdown, characterDropdown, regionDropdown;
        [SerializeField] private Button createRoomBtn, joinRoomBtn, joinPrivateRoomBtn, reconnectBtn, startGameBtn;
        [SerializeField] private TMP_InputField nicknameField, chatTextField;
        [SerializeField] private TMP_Text lobbyHeaderText, updateText, startGameButtonText;
        [SerializeField] private ScrollRect settingsScroll;
        [SerializeField] private Slider lobbyPlayersSlider;
        [SerializeField] private CanvasGroup hostControlsGroup, copyRoomIdCanvasGroup, roomListCanvasGroup, joinStartButtonCanvasGroup;
        [SerializeField] private ErrorPrompt errorPrompt, networkErrorPrompt;

        [SerializeField, FormerlySerializedAs("ColorBar")] private Image colorBar;
        [SerializeField] private Image overallsColorImage, shirtColorImage;
        [SerializeField] private GameObject playerColorPaletteIcon, playerColorDisabledIcon;

        [Header("Misc")]
        [SerializeField] public List<MapData> maps;

        //---Private Variables
        private CharacterAsset currentCharacter;
        private Coroutine quitCoroutine, fadeMusicCoroutine;
        private int lastCountdownStartFrame;
        private bool wasSettingsOpen, startingGame, alreadyInRoom;

        public void Awake() {
            Set(this, false);
        }

        public void OnEnable() {
            // Register callbacks
            //PlayerData.OnPlayerDataReady += OnPlayerDataReady;
            //PlayerData.OnPlayerDataDespawned += OnPlayerDataDespawned;
            //NetworkHandler.OnLobbyConnect += OnLobbyConnect;
            //NetworkHandler.OnShutdown += OnShutdown;
            //NetworkHandler.OnDisconnectedFromServer += OnDisconnect;
            //NetworkHandler.OnConnectFailed += OnConnectFailed;
            //NetworkHandler.OnRegionPingsUpdated += OnRegionPingsUpdated;
            //MvLSceneManager.OnSceneLoadStart += OnSceneLoadStart;
            NetworkHandler.StateChanged += OnClientStateChanged;

            ControlSystem.controls.UI.Pause.performed += OnPause;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            // Unregister callbacks
            //PlayerData.OnPlayerDataReady -= OnPlayerDataReady;
            //PlayerData.OnPlayerDataDespawned -= OnPlayerDataDespawned;
            //NetworkHandler.OnLobbyConnect -= OnLobbyConnect;
            //NetworkHandler.OnShutdown -= OnShutdown;
            //NetworkHandler.OnDisconnectedFromServer -= OnDisconnect;
            //NetworkHandler.OnConnectFailed -= OnConnectFailed;
            //NetworkHandler.OnRegionPingsUpdated -= OnRegionPingsUpdated;
            //MvLSceneManager.OnSceneLoadStart -= OnSceneLoadStart;
            NetworkHandler.StateChanged -= OnClientStateChanged;

            ControlSystem.controls.UI.Pause.performed -= OnPause;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            // Clear game-specific settings so they don't carry over
            /* TODO
            HorizontalCamera.SizeIncreaseTarget = 0;
            HorizontalCamera.SizeIncreaseCurrent = 0;
            */

            PreviewLevel(UnityEngine.Random.Range(0, maps.Count));
            //UpdateRegionDropdown();
            //StartCoroutine(NetworkHandler.PingRegions());

            // Multiplayer stuff
            if (GlobalController.Instance.firstConnection) {
                OpenTitleScreen();
            }

            // Controls & Settings
            nicknameField.text = Settings.Instance.generalNickname;
            nicknameField.characterLimit = NicknameMax;
            UpdateNickname();

            // Discord RPC
            GlobalController.Instance.discordController.UpdateActivity();

            // Set up room list
            roomManager.Initialize();

#if PLATFORM_WEBGL
            copyRoomIdCanvasGroup.interactable = false;
#else
            // Version Checking
            if (!GlobalController.Instance.checkedForVersion) {
                UpdateChecker.IsUpToDate((upToDate, latestVersion) => {
                    if (upToDate) {
                        return;
                    }

                    updateText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.update.prompt", "newversion", latestVersion, "currentversion", Application.version);
                    updateBox.SetActive(true);
                    EventSystem.current.SetSelectedGameObject(updateBoxSelected);
                });
                GlobalController.Instance.checkedForVersion = true;
            }
#endif

            GlobalController.Instance.firstConnection = false;

            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded, onlyIfActiveAndEnabled: true);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved, onlyIfActiveAndEnabled: true);
            QuantumEvent.Subscribe<EventStartingCountdownChanged>(this, OnCountdownChanged);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventHostChanged>(this, OnHostChanged);
            QuantumEvent.Subscribe<EventCountdownTick>(this, OnCountdownTick);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumCallback.Subscribe<CallbackGameInit>(this, OnGameInit);
            QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerConfirmed);
        }

        public void Update() {
            wasSettingsOpen = GlobalController.Instance.optionsManager.gameObject.activeSelf;
        }

        public void UpdateRegionDropdown() {
            if (NetworkHandler.Regions == null) {
                return;
            }

            if (regionDropdown.options.Count == 0) {
                // Create brand-new options
                for (int i = 0; i < NetworkHandler.Regions.Count; i++) {
                    Region region = NetworkHandler.Regions[i];
                    regionDropdown.options.Add(new RegionOption(i, region.Code, region.Ping));
                }
                regionDropdown.options.Sort();
            } else {
                // Update existing options
                RegionOption selected = (RegionOption) regionDropdown.options[regionDropdown.value];

                if (NetworkHandler.Regions != null) {
                    foreach (var option in regionDropdown.options) {
                        if (option is RegionOption ro) {
                            ro.Ping = NetworkHandler.Regions[ro.Index].Ping;
                        }
                    }
                }

                regionDropdown.options.Sort();
                regionDropdown.SetValueWithoutNotify(regionDropdown.options.IndexOf(selected));
            }
        }

        public unsafe void EnterRoom() {
            // Chat
            chatTextField.SetTextWithoutNotify("");
            
            // Open the in-room menu
            OpenInRoomMenu();

            // Host reminder
            SendHostReminder();

            // Fix the damned setting scroll menu
            StartCoroutine(SetVerticalNormalizedPositionFix(settingsScroll, 1));

            // Set the room settings
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(f, out _);
            bool isHost = game.PlayerIsLocal(hostPlayer);
            hostControlsGroup.interactable = isHost;

            roomSettingsCallbacks.RefreshSettingsUI(f, false);

            // Set the player settings
            SwapCharacter(Settings.Instance.generalCharacter, false);
            SwapPlayerSkin(Settings.Instance.generalSkin, false);
            spectateToggle.isOn = false;

            // Reset the "Game start" button counting down
            OnCountdownTick(-1);

            // Update the room header text + color
            UpdateRoomHeader();

            // Discord RPC
            GlobalController.Instance.discordController.UpdateActivity();

            // Create player icons
            playerList.PopulatePlayerEntries(QuantumRunner.DefaultGame);
        }

        public void UpdateRoomHeader() {
            const int rngSeed = 2035767;

            Room room = NetworkHandler.Client.CurrentRoom;
            string hostname = room.Players[room.MasterClientId].NickName.ToValidUsername();

            lobbyHeaderText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", hostname.ToValidUsername());
            UnityEngine.Random.InitState(hostname.GetHashCode() + rngSeed);
            colorBar.color = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0f, 1f);
        }

        private static IEnumerator SetVerticalNormalizedPositionFix(ScrollRect scroll, float value) {
            for (int i = 0; i < 3; i++) {
                scroll.verticalNormalizedPosition = value;
                Canvas.ForceUpdateCanvases();
                yield return null;
            }
        }

        public void PreviewLevel(int levelIndex) {
            if (levelIndex < 0 || levelIndex >= maps.Count) {
                levelIndex = 0;
            }

            Camera.main.transform.position = maps[levelIndex].levelPreviewPosition.transform.position;
        }

        private void DisableAllMenus() {
            title.SetActive(false);
            bg.SetActive(false);
            mainMenu.SetActive(false);
            lobbyMenu.SetActive(false);
            createLobbyPrompt.SetActive(false);
            webglCreateLobbyPrompt.SetActive(false);
            inLobbyMenu.SetActive(false);
            creditsMenu.SetActive(false);
            privateRoomIdPrompt.SetActive(false);
            updateBox.SetActive(false);
        }

        public void OpenTitleScreen() {
            DisableAllMenus();
            title.SetActive(true);

            EventSystem.current.SetSelectedGameObject(titleSelected);
        }
        public void OpenMainMenu() {
            DisableAllMenus();
            bg.SetActive(true);
            mainMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(mainMenuSelected);
        }
        public void OpenRoomListMenu() {
            DisableAllMenus();
            bg.SetActive(true);
            lobbyMenu.SetActive(true);
            chat.ClearChat();

            // First connection on play game button.
            if (NetworkHandler.Client.State == ClientState.PeerCreated) {
                _ = Reconnect();
            }

            roomManager.RefreshRooms();
            EventSystem.current.SetSelectedGameObject(lobbySelected);
        }

        public void TryOpenCreateRoomPrompt() {
#if PLATFORM_WEBGL
            DisableAllMenus();
            bg.SetActive(true);
            lobbyMenu.SetActive(true);
            webglCreateLobbyPrompt.SetActive(true);
#else
            OpenCreateRoomPrompt();
#endif
        }

        public void OpenCreateRoomPrompt() {
            DisableAllMenus();
            bg.SetActive(true);
            lobbyMenu.SetActive(true);
            createLobbyPrompt.SetActive(true);
        }

        public void OpenOptions() {
            if (wasSettingsOpen) {
                return;
            }

            GlobalController.Instance.optionsManager.OpenMenu();
        }

        public void OpenCredits() {
            DisableAllMenus();
            bg.SetActive(true);
            creditsMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(creditsSelected);
        }

        public void OpenInRoomMenu() {
            DisableAllMenus();
            bg.SetActive(true);
            inLobbyMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(currentLobbySelected);
        }

        public void OpenErrorBox(short cause) {
             OpenErrorBox(NetworkUtils.DisconnectMessages.GetValueOrDefault(cause, $"Unknown error (Code: {cause})"));
        }

        public void OpenErrorBox(string key, params string[] replacements) {
            errorPrompt.OpenWithText(key, replacements);
            nonNetworkShutdown = false;
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        public void OpenNetworkErrorBox(string key) {
            networkErrorPrompt.OpenWithText(key);
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        public void OpenNetworkErrorBox(short cause) {
            if (nonNetworkShutdown) {
                OpenErrorBox(cause);
                return;
            }

            OpenErrorBox(NetworkUtils.DisconnectMessages.GetValueOrDefault(cause, $"Unknown error (Code: {cause})"));
        }

        public void BackSound() {
            sfx.PlayOneShot(SoundEffect.UI_Back);
        }

        public void ConfirmSound() {
            sfx.PlayOneShot(SoundEffect.UI_Decide);
        }

        public void CursorSound() {
            sfx.PlayOneShot(SoundEffect.UI_Cursor);
        }

        public void StartSound() {
            sfx.PlayOneShot(SoundEffect.UI_StartGame);
        }

        public void ConnectToDropdownRegion() {
            RegionOption selectedRegion = (RegionOption) regionDropdown.options[regionDropdown.value];
            string targetRegion = selectedRegion.Region;
            if (NetworkHandler.Region == targetRegion) {
                return;
            }

            roomManager.ClearRooms();
            _ = NetworkHandler.ConnectToRegion(targetRegion);
        }

        public async Task<bool> Reconnect() {
            GlobalController.Instance.connecting.SetActive(true);
            roomListCanvasGroup.interactable = false;
            return await NetworkHandler.ConnectToRegion(null);
        }

        public void QuitRoom() {
            QuantumRunner.Default.Shutdown();
        }

        public unsafe void StartCountdown() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);

            if (game.PlayerIsLocal(host)) {
                int hostSlot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)];
                game.SendCommand(hostSlot, new CommandToggleCountdown());
            } else {
                foreach (int slot in game.GetLocalPlayerSlots()) {
                    // All players are ready at the same time
                    game.SendCommand(slot, new CommandToggleReady());
                }

                List<PlayerRef> localPlayers = game.GetLocalPlayers();
                bool isReady = false;
                if (localPlayers.Count > 0) {
                    var playerData = QuantumUtils.GetPlayerData(f, localPlayers[0]);
                    isReady = playerData->IsReady;
                }

                sfx.PlayOneShot(isReady ? SoundEffect.UI_Back : SoundEffect.UI_Decide);
                UpdateReadyButton(!isReady);
            }
        }

        private IEnumerator FadeMusic() {
            while (music.volume > 0) {
                music.volume -= Time.deltaTime;
                yield return null;
            }
        }

        public void UpdateReadyButton(bool ready) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            startGameButtonText.text = tm.GetTranslation(ready ? "ui.inroom.buttons.unready" : "ui.inroom.buttons.readyup");
        }

        public unsafe void UpdateStartGameButton() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);
            bool weAreHost = game.PlayerIsLocal(host);

            TranslationManager tm = GlobalController.Instance.translationManager;
            
            if (f.Global->GameStartFrames > 0) {
                return;
            }

            if (weAreHost) {
                startGameButtonText.text = tm.GetTranslation("ui.inroom.buttons.start");
                startGameBtn.interactable = IsRoomConfigurationValid();
            } else {
                List<PlayerRef> localPlayers = game.GetLocalPlayers();
                if (localPlayers.Count > 0) {
                    var playerData = QuantumUtils.GetPlayerData(f, localPlayers[0]);
                    if (playerData != null) {
                        UpdateReadyButton(playerData->IsReady);
                    }
                    startGameBtn.interactable = true;
                }
            }
        }

        public unsafe bool IsRoomConfigurationValid() {
            var filter = QuantumRunner.DefaultGame.Frames.Predicted.Filter<PlayerData>();

            while (filter.NextUnsafe(out _, out PlayerData* data)) {
                if (!data->IsSpectator) {
                    return true;
                }
            }

            return false;
        }

        /* TODO
        public void Kick(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            SessionData.Instance.Disconnect(target.Owner);
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.kicked", ChatManager.Blue, "playername", target.GetNickname());
        }
        */

        /* TODO
        public void Promote(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            if (Runner.Topology == Topologies.ClientServer) {
                ChatManager.Instance.AddSystemMessage("Cannot promote yet!", ChatManager.Red);
            } else {
                Runner.SetMasterClient(target.Owner);
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.promoted", ChatManager.Blue, "playername", target.GetNickname());
            }
        }
        */

        /* TODO
        public void Mute(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            bool newMuteState = !target.IsMuted;
            target.IsMuted = newMuteState;
            ChatManager.Instance.AddSystemMessage(newMuteState ? "ui.inroom.chat.player.muted" : "ui.inroom.chat.player.unmuted", ChatManager.Blue, "playername", target.GetNickname());
        }
        */

        /* TODO
        public void Ban(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            SessionData.Instance.Disconnect(target.Owner);
            SessionData.Instance.AddBan(target);
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.banned", ChatManager.Blue, "playername", target.GetNickname());
        }
        */

        public void UI_CharacterDropdownChanged() {
            int value = characterDropdown.value;
            SwapCharacter(value, true);

            CharacterAsset data = GlobalController.Instance.config.CharacterDatas[value];
            sfx.PlayOneShot(SoundEffect.Player_Voice_Selected, data);
        }

        public void SwapCharacter(int character, bool broadcast) {
            if (broadcast) {
                QuantumRunner.DefaultGame.SendCommand(new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Character,
                    Character = (byte) character
                });
            } else {
                characterDropdown.SetValueWithoutNotify(character);
            }

            Settings.Instance.generalCharacter = character;
            Settings.Instance.SaveSettings();

            currentCharacter = GlobalController.Instance.config.CharacterDatas[character];
            colorManager.ChangeCharacter(currentCharacter);
            SwapPlayerSkin(currentSkin, false);
        }

        public void SwapPlayerSkin(int index, bool save) {
            bool disabled = index == 0;

            if (!disabled) {
                playerColorDisabledIcon.SetActive(false);
                playerColorPaletteIcon.SetActive(true);
                PlayerColorSet set = ScriptableManager.Instance.skins[index];
                PlayerColors colors = set.GetPlayerColors(currentCharacter);
                overallsColorImage.color = colors.overallsColor;
                shirtColorImage.color = colors.shirtColor;
                ColorName.GetComponent<TMP_Text>().text = set.Name;
            }

            playerColorDisabledIcon.SetActive(disabled);
            playerColorPaletteIcon.SetActive(!disabled);

            if (save) {
                Settings.Instance.generalSkin = index;
                Settings.Instance.SaveSettings();
            }

            currentSkin = index;
        }

        public void EnableSpectator(Toggle toggle) {
            QuantumRunner.DefaultGame.SendCommand(new CommandChangePlayerData {
                EnabledChanges = CommandChangePlayerData.Changes.Spectating,
                Spectating = toggle.isOn,
            });
        }


        private void UpdateNickname() {
            bool validUsername = Settings.Instance.generalNickname.IsValidUsername();
            ColorBlock colors = nicknameField.colors;
            if (validUsername) {
                colors.normalColor = Color.white;
                colors.highlightedColor = new(0.7f, 0.7f, 0.7f, 1);
            } else {
                colors.normalColor = new(1, 0.7f, 0.7f, 1);
                colors.highlightedColor = new(1, 0.55f, 0.55f, 1);
            }

            nicknameField.colors = colors;
            joinStartButtonCanvasGroup.interactable = validUsername && NetworkHandler.Client.IsConnectedAndReady;
            NetworkHandler.Client.NickName = Settings.Instance.generalNickname;
        }

        public void SetUsername(TMP_InputField field) {
            Settings.Instance.generalNickname = field.text;
            UpdateNickname();
            Settings.Instance.SaveSettings();
        }

        public void OpenLinks() {
            Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/blob/master/LINKS.md");
        }

        public void Quit() {
            if (quitCoroutine == null) {
                quitCoroutine = StartCoroutine(FinishQuitting());
            }
        }

        private IEnumerator FinishQuitting() {
            AudioClip clip = SoundEffect.UI_Quit.GetClip();
            sfx.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OpenDownloadsPage() {
            Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/releases/latest");
            OpenMainMenu();
        }

        private unsafe void SendHostReminder() {
            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _);

            if (game.PlayerIsLocal(hostPlayer)) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.hostreminder", ChatManager.Red);
            }
        }

        public unsafe void OnCountdownTick(int time) {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(f, out _);
            bool weAreHost = game.PlayerIsLocal(hostPlayer);

            TranslationManager tm = GlobalController.Instance.translationManager;
            if (time > 0) {
                startGameBtn.interactable = weAreHost;
                startGameButtonText.text = tm.GetTranslationWithReplacements("ui.inroom.buttons.starting", "countdown", time.ToString());
                hostControlsGroup.interactable = false;
                if (time == 1 && fadeMusicCoroutine == null) {
                    fadeMusicCoroutine = StartCoroutine(FadeMusic());
                }
            } else {
                UpdateStartGameButton();
                hostControlsGroup.interactable = weAreHost;
                if (fadeMusicCoroutine != null) {
                    StopCoroutine(fadeMusicCoroutine);
                    fadeMusicCoroutine = null;
                }
                music.volume = 1;
            }

            startGameButtonText.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }

        //---Callbacks
        /* TODO
        public void OnLobbyConnect(NetworkRunner runner, LobbyInfo info) {
            for (int i = 0; i < regionDropdown.options.Count; i++) {
                RegionOption option = (RegionOption) regionDropdown.options[i];
                if (option.Region != info.Region) {
                    continue;
                }

                regionDropdown.SetValueWithoutNotify(i);
                return;
            }
        }

        private void OnShutdown(NetworkRunner runner, ShutdownReason cause) {
            if (cause != ShutdownReason.Ok) {
                OpenNetworkErrorBox(cause);
            }

            if (inLobbyMenu.activeSelf) {
                OpenRoomListMenu();
            }

            music.volume = 1;
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        private void OnDisconnect(NetworkRunner runner, NetDisconnectReason disconnectReason) {
            OpenNetworkErrorBox(disconnectReason);
            OpenRoomListMenu();
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        private void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason cause) {
            OpenErrorBox(cause);

            if (!runner.IsCloudReady) {
                roomManager.ClearRooms();
            }
        }
        */

        private void OnLocalPlayerConfirmed(CallbackLocalPlayerAddConfirmed e) {
            QuantumGame game = e.Game;
            int slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(e.Player)];

            QuantumRunner.DefaultGame.SendCommand(slot, new CommandChangePlayerData {
                EnabledChanges = CommandChangePlayerData.Changes.All,
                Character = (byte) Settings.Instance.generalCharacter,
                Skin = (byte) Settings.Instance.generalSkin,
                Spectating = false,
                Team = 0,
            });

            if (e.Game.PlayerIsLocal(e.Player) && !alreadyInRoom) {
                EnterRoom();
                alreadyInRoom = true;
            }
        }

        private void OnClientStateChanged(ClientState oldState, ClientState newState) {
            switch (newState) {
            case ClientState.DisconnectingFromNameServer:
                // Add regions to dropdown
                UpdateRegionDropdown();
                break;
            case ClientState.ConnectedToMasterServer:
                // Change region dropdown
                int index =
                    regionDropdown.options
                        .Cast<RegionOption>()
                        .IndexOf(ro => ro.Region == NetworkHandler.Region);

                if (index != -1) {
                    regionDropdown.SetValueWithoutNotify(index);
                    regionDropdown.RefreshShownValue();
                }
                break;
            }

            roomListCanvasGroup.interactable = newState == ClientState.JoinedLobby || newState == ClientState.Disconnected;
            reconnectBtn.gameObject.SetActive(newState == ClientState.Disconnected);
            joinPrivateRoomBtn.gameObject.SetActive(newState == ClientState.JoinedLobby);
            UpdateNickname();
        }

        private void OnLanguageChanged(TranslationManager tm) {
            int selectedLevel = levelDropdown.value;
            levelDropdown.ClearOptions();
            levelDropdown.AddOptions(maps.Select(map => tm.GetTranslation(map.translationKey)).ToList());
            levelDropdown.SetValueWithoutNotify(selectedLevel);

            int selectedCharacter = characterDropdown.value;
            characterDropdown.ClearOptions();
            foreach (CharacterAsset character in GlobalController.Instance.config.CharacterDatas) {
                string characterName = tm.GetTranslation(character.TranslationString);
                characterDropdown.options.Add(new TMP_Dropdown.OptionData(characterName, character.ReadySprite));
            }
            characterDropdown.SetValueWithoutNotify(selectedCharacter);
            characterDropdown.RefreshShownValue();

            /* TODO
            if (SessionData.Instance && SessionData.Instance.Object) {
                UpdateRoomHeader();
                OnCountdownTick((int) (SessionData.Instance.GameStartTimer.RemainingRenderTime(NetworkHandler.Runner) ?? -1));
            }
            */
        }

        private void OnPause(InputAction.CallbackContext context) {
            if (isActiveAndEnabled && (NetworkHandler.Client?.InRoom ?? false) && !wasSettingsOpen) {
                // Open the settings menu if we're inside a room (so we dont have to leave)
                // ConfirmSound();
                OpenOptions();
            }
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.WaitingForPlayers) {
                GlobalController.Instance.loadingCanvas.Initialize(e.Game);
                transform.parent.gameObject.SetActive(false);
            } else if (e.NewState == GameState.PreGameRoom) {
                transform.parent.gameObject.SetActive(true);
            }
        }

        private void OnGameInit(CallbackGameInit e) {
            alreadyInRoom = false;
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            alreadyInRoom = false;
            OpenRoomListMenu();
            GlobalController.Instance.discordController.UpdateActivity();
            _ = Reconnect();
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            sfx.PlayOneShot(SoundEffect.UI_PlayerConnect);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            sfx.PlayOneShot(SoundEffect.UI_PlayerDisconnect);
        }

        private unsafe void OnCountdownChanged(EventStartingCountdownChanged e) {
            Frame f = e.Frame;
            PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);
            OnCountdownTick(e.IsGameStarting ? 3 : -1);

            if (e.Game.PlayerIsLocal(host)
                || (startingGame && !e.IsGameStarting)
                || f.Number - lastCountdownStartFrame > 60 * 3) {
                // Play the sound (and send the chat message)

                sfx.PlayOneShot(e.IsGameStarting ? SoundEffect.UI_FileSelect : SoundEffect.UI_Back);

                if (e.IsGameStarting) {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.starting", ChatManager.Red, "countdown", "3");
                } else {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.startcancelled", ChatManager.Red);
                }
                startingGame = e.IsGameStarting;
            }
            lastCountdownStartFrame = f.Number;
        }

        private void OnCountdownTick(EventCountdownTick e) {
            OnCountdownTick(e.SecondsRemaining);
        }

        private void OnHostChanged(EventHostChanged e) {
            SendHostReminder();
            hostControlsGroup.interactable = e.Game.PlayerIsLocal(e.NewHost);
        }

        //---Debug
#if UNITY_EDITOR
        private static readonly Vector3 MaxCameraSize = new(16f/9f * 7f, 7f);

        public void OnDrawGizmos() {
            Gizmos.color = Color.red;
            foreach (MapData map in maps) {
                if (map.levelPreviewPosition) {
                    Gizmos.DrawWireCube(map.levelPreviewPosition.transform.position, MaxCameraSize);
                }
            }
        }
#endif

        //---Helpers
        [Serializable]
        public class MapData {
            public string translationKey;
            public GameObject levelPreviewPosition;
            public AssetRef<Map> mapAsset;
        }

        public class RegionOption : TMP_Dropdown.OptionData, IComparable {
            public int Index { get; }
            public string Region { get; }
            private int _ping = -1;
            public int Ping {
                get => _ping;
                set {
                    if (value <= 0) {
                        value = -1;
                    }

                    _ping = value;
                    text = "<align=left>" + Region + "<line-height=0>\n<align=right>" + Utils.Utils.GetPingSymbol(_ping);
                }
            }

            public RegionOption(int index, string region, int ping) {
                Index = index;
                Region = region;
                Ping = ping;
            }

            public int CompareTo(object other) {
                if (other is not RegionOption ro) {
                    return -1;
                }

                if (Ping <= 0) {
                    return 1;
                }

                if (ro.Ping <= 0) {
                    return -1;
                }

                return Ping.CompareTo(ro.Ping);
            }
        }
    }
}

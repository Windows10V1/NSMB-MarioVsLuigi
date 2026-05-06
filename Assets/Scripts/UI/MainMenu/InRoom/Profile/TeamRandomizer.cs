using JimmysUnityUtilities;
using NSMB.Utilities;
using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class TeamRandomizer : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject blockerTemplate;
        [SerializeField] public GameObject content;
        [SerializeField] private TeamButton[] buttons;
        [SerializeField] private Button button;
        [SerializeField] private Image flag;
        [SerializeField] private Sprite disabledSprite;

        //---Private Variables
        private GameObject blockerInstance;
        private int selected;

        public void Initialize() {
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
            QuantumEvent.Subscribe<EventPlayerTeamChangedByHost>(this, OnPlayerTeamChangedByHost);
        }

        public void OnEnable() {
            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                OnRulesChanged(new EventRulesChanged {
                    Game = game,
                    Tick = game.Frames.Predicted.Number,
                });
            }
        }

        public void OnDisable() {
            Close(false);
        }

        public void SetEnabled(bool value) {
            button.interactable = value;

            Close(true);
        }

        public unsafe void RandomizeTeam(TeamButton team) {
            var game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            selected = team.index + 1;

            var teams = AssetRepository<TeamAsset>.AllAssets;
            byte[] numTeams = new byte[selected];

            // select two random colors to be the teams
            for (int i = 0; i < numTeams.Length; i++) {
                byte checkTeam = (byte)f.RNG->Next(0, teams.Count);
                bool teamExists = numTeams.IndexOf(checkTeam) != -1;

                if (teamExists) {
                    continue;
                }

                numTeams[i] = checkTeam;
            }

            foreach (int slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandRandomizeTeam {
                    Teams = numTeams,
                });
            }

            TeamAsset teamScriptable = teams[selected % teams.Count];
            flag.sprite = Settings.Instance.GraphicsColorblind ? teamScriptable.spriteColorblind : teamScriptable.spriteNormal;
            canvas.PlayConfirmSound();
            canvas.EventSystem.SetSelectedGameObject(button.gameObject);
        }

        public unsafe void Open() {
            var game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

            int selected = Mathf.Clamp(playerData->RequestedTeam, 0, AssetRepository<TeamAsset>.AllAssetRefs.Count);

            blockerInstance = Instantiate(blockerTemplate, canvas.transform);
            blockerInstance.SetActive(true);
            content.SetActive(true);

            canvas.PlayCursorSound();
            canvas.EventSystem.SetSelectedGameObject(buttons[selected].gameObject);
        }

        public void Close(bool playSound) {
            if (!blockerInstance) {
                return;
            }

            Destroy(blockerInstance);
            canvas.EventSystem.SetSelectedGameObject(button.gameObject);
            content.SetActive(false);

            if (playSound) {
                canvas.PlaySound(SoundEffect.UI_Back);
            }
        }

        private unsafe void UpdateButtonInteractable(QuantumGame game) {
            Frame f = game.Frames.Predicted;

            if (f.Global->Rules.TeamsEnabled) {
                var teams = f.Context.GetAllAssets<TeamAsset>();
                TeamAsset team = teams[selected % teams.Count];
                flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
            } else {
                flag.sprite = disabledSprite;
            }
        }

        private unsafe void OnColorblindModeChanged() {
            var game = QuantumRunner.DefaultGame;
            if (game == null) {
                return;
            }

            Frame f = game.Frames.Predicted;
            if (f.Global->Rules.TeamsEnabled) {
                var teams = f.Context.GetAllAssets<TeamAsset>();
                TeamAsset team = teams[selected % teams.Count];
                flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            UpdateButtonInteractable(e.Game);
        }

        private unsafe void OnPlayerTeamChangedByHost(EventPlayerTeamChangedByHost e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                UpdateButtonInteractable(e.Game);

                if (!e.Clear) {
                    Close(false);
                }
            }
        }

        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            if (!e.Game.PlayerIsLocal(e.Player)) {
                return;
            }

            Frame f = e.Game.Frames.Predicted;
            var playerData = QuantumUtils.GetPlayerData(f, e.Player);
            selected = playerData->RequestedTeam;

            if (f.Global->Rules.TeamsEnabled) {
                var teams = f.Context.GetAllAssets<TeamAsset>();
                TeamAsset team = teams[selected % teams.Count];
                flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
            }
        }
    }
}

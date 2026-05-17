using NSMB.Utilities;
using Quantum;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class TeamRandomizer : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject blockerTemplate;
        [SerializeField] public GameObject content;
        [SerializeField] private TeamRandButton[] buttons;
        [SerializeField] private Button button;
        [SerializeField] private Image flag;
        [SerializeField] private Sprite enabledSprite, disabledSprite;

        //---Private Variables
        private GameObject blockerInstance;

        public void Initialize() {
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
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

        public unsafe void RandomizeTeam(TeamRandButton team) {
            var game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef host = f.Global->Host;

            game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], new CommandRandomizeAllTeams {
                Teams = team.teamCount
            });

            Close(false);
            canvas.PlayConfirmSound();
            canvas.EventSystem.SetSelectedGameObject(button.gameObject);
        }

        public unsafe void Open() {
            var game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef host = f.Global->Host;

            if (!game.PlayerIsLocal(host)) {
                canvas.PlaySound(SoundEffect.UI_Error);
                return;
            }

            blockerInstance = Instantiate(blockerTemplate, canvas.transform);
            blockerInstance.SetActive(true);
            content.SetActive(true);

            canvas.PlayCursorSound();
            canvas.EventSystem.SetSelectedGameObject(buttons[0].gameObject);
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
                flag.sprite = enabledSprite;
                button.interactable = true;
            } else {
                flag.sprite = disabledSprite;
                button.interactable = false;
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            UpdateButtonInteractable(e.Game);
        }
    }
}

using NSMB.UI.Translation;
using Quantum;
using System.Collections.Generic;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {

    public class PowerupChangeableRule : ChangeableRule {

        //---Constants
        private const string NoneTranslationKey = "ui.generic.none";

        //---Properties
        public override bool CanIncreaseValue => FindCurrentIndex() < GetOptions().Count - 1;
        public override bool CanDecreaseValue => FindCurrentIndex() > 0;

        //---Private Variables
        private readonly List<PowerupOption> options = new();
        private AssetRef<GamemodeAsset> optionsGamemode;
        private bool optionsBuilt;

        protected override void IncreaseValueInternal() {
            ChangeSelection(+1);
        }

        protected override void DecreaseValueInternal() {
            ChangeSelection(-1);
        }

        private void ChangeSelection(int direction) {
            List<PowerupOption> allOptions = GetOptions();
            int currentIndex = FindCurrentIndex();
            int newIndex = Mathf.Clamp(currentIndex + direction, 0, allOptions.Count - 1);

            if (newIndex != currentIndex) {
                value = (byte) allOptions[newIndex].State;
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            if (value is not byte byteValue) {
                return;
            }

            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
                StartingPowerup = (PowerupState) byteValue,
            };

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (game.PlayerIsLocal(host)) {
                game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);
            }
        }

        protected override void UpdateLabel() {
            if (value is not byte byteValue) {
                return;
            }

            TranslationManager tm = GlobalController.Instance.translationManager;
            PowerupState state = (PowerupState) byteValue;

            string text;
            if (state == PowerupState.NoPowerup) {
                text = tm.TryGetTranslation(NoneTranslationKey, out string noneText) ? noneText : "None";
            } else {
                string key = null;
                foreach (PowerupOption option in GetOptions()) {
                    if (option.State == state) {
                        key = option.TranslationKey;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(key) && tm.TryGetTranslation(key, out string powerupText)) {
                    text = powerupText;
                } else {
                    text = state.ToString();
                }
            }

            label.text = labelPrefix + text + labelSuffix;
        }

        private int FindCurrentIndex() {
            if (value is not byte byteValue) {
                return 0;
            }

            List<PowerupOption> allOptions = GetOptions();
            for (int i = 0; i < allOptions.Count; i++) {
                if ((byte) allOptions[i].State == byteValue) {
                    return i;
                }
            }
            return 0;
        }

        private unsafe List<PowerupOption> GetOptions() {
            AssetRef<GamemodeAsset> gamemodeRef = default;
            QuantumGame game = QuantumRunner.DefaultGame;
            if (game != null && game.Frames.Predicted != null) {
                gamemodeRef = game.Frames.Predicted.Global->Rules.Gamemode;
            }

            if (optionsBuilt && optionsGamemode == gamemodeRef) {
                return options;
            }

            options.Clear();
            options.Add(new PowerupOption(PowerupState.NoPowerup, null));

            if (QuantumUnityDB.TryGetGlobalAsset(gamemodeRef, out GamemodeAsset gamemode)) {
                SortedDictionary<byte, PowerupOption> found = new();
                foreach (AssetRef<CoinItemAsset> itemRef in gamemode.AllCoinItems) {
                    if (!QuantumUnityDB.TryGetGlobalAsset(itemRef, out CoinItemAsset item)
                        || item is not PowerupAsset powerup
                        || powerup.State == PowerupState.NoPowerup
                        || powerup.State == PowerupState.MegaMushroom) {
                        continue;
                    }

                    if (!found.ContainsKey((byte) powerup.State)) {
                        found.Add((byte) powerup.State, new PowerupOption(powerup.State, powerup.TranslationKey));
                    }
                }
                options.AddRange(found.Values);
            }

            optionsGamemode = gamemodeRef;
            optionsBuilt = true;
            return options;
        }

        private readonly struct PowerupOption {
            public readonly PowerupState State;
            public readonly string TranslationKey;

            public PowerupOption(PowerupState state, string translationKey) {
                State = state;
                TranslationKey = translationKey;
            }
        }
    }
}

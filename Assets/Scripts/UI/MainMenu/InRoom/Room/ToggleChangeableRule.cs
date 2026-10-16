using NSMB.UI.Translation;
using Quantum;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ToggleChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue => value is bool b && !b;
        public override bool CanDecreaseValue => value is bool b2 && b2;

        protected override void IncreaseValueInternal() {
            if (value is bool bv && !bv) {
                value = true;
                cursorSfx.Play();
                SendCommand();
            }
        }

        protected override unsafe void DecreaseValueInternal() {
            if (value is bool bv && bv) {
                value = false;
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
            };

            if (value is not bool boolValue) {
                return;
            }

            switch (ruleType) {
            case CommandChangeRules.Rules.CustomPowerupsEnabled:
                cmd.CustomPowerupsEnabled = boolValue;
                break;
            case CommandChangeRules.Rules.TeamsEnabled:
                cmd.TeamsEnabled = boolValue;
                break;
            case CommandChangeRules.Rules.StarCoinsEnabled:
                cmd.StarCoinsEnabled = boolValue;
                break;
            case CommandChangeRules.Rules.CharacterSpecialPhysicsEnabled:
                cmd.CharacterSpecialPhysicsEnabled = boolValue;
                break;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            
            if (game.PlayerIsLocal(host)) {
                game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);
            }
        }

        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is bool boolValue) {
                label.text = labelPrefix + tm.GetTranslation(boolValue ? "ui.generic.on" : "ui.generic.off");
            }
        }
    }
}

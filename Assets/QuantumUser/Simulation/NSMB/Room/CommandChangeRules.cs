using Photon.Deterministic;
using System;

namespace Quantum {
    public class CommandChangeRules : DeterministicCommand, ILobbyCommand {

        public Rules EnabledChanges;

        public AssetRef<Map> Stage;
        public AssetRef<GamemodeAsset> Gamemode;
        public int StarsToWin;
        public int CoinsForPowerup;
        public int Lives;
        public int TimerMinutes;
        public bool TeamsEnabled;
        public bool CustomPowerupsEnabled;
        public int StarFountain;
        public bool StarCoinsEnabled;
        public bool CharacterSpecialPhysicsEnabled;
        public StageChooseMode ChooseMode;
        public int TeamAttack;

        public override void Serialize(BitStream stream) {
            if (stream.Writing) {
                stream.WriteUShort((ushort) EnabledChanges);
            } else {
                EnabledChanges = (Rules) stream.ReadUShort();
            }

            stream.Serialize(ref Stage);
            stream.Serialize(ref Gamemode);
            stream.Serialize(ref StarsToWin);
            stream.Serialize(ref CoinsForPowerup);
            stream.Serialize(ref Lives);
            stream.Serialize(ref TimerMinutes);
            stream.Serialize(ref TeamsEnabled);
            stream.Serialize(ref CustomPowerupsEnabled);
            stream.Serialize(ref StarFountain);
            stream.Serialize(ref StarCoinsEnabled);
            stream.Serialize(ref CharacterSpecialPhysicsEnabled);

            if (stream.Writing) {
                stream.WriteByte((byte) ChooseMode);
            } else {
                ChooseMode = (StageChooseMode) stream.ReadByte();
            }

            stream.Serialize(ref TeamAttack);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost(f)) {
                // Only the host can change rules.
                return;
            }

            Rules rulesChanges = EnabledChanges;
            var rules = f.Global->Rules;
            bool gamemodeChanged = false;
            bool levelChanged = false;

            if (rulesChanges.HasFlag(Rules.Gamemode)) {
                gamemodeChanged = rules.Gamemode != Gamemode;

                GameRules newRules = default;
                f.FindAsset(Gamemode).DefaultRules.Materialize(f, ref newRules);
                newRules.Stage = rules.Stage;
                newRules.ChooseMode = rules.ChooseMode;
                newRules.RandomDisabledStages = rules.RandomDisabledStages;

                rules = newRules;
            }
            if (rulesChanges.HasFlag(Rules.Stage)) {
                levelChanged = rules.Stage != Stage;
                rules.Stage = Stage;
            }
            if (rulesChanges.HasFlag(Rules.StarsToWin)) {
                rules.StarsToWin = StarsToWin;
            }
            if (rulesChanges.HasFlag(Rules.CoinsForPowerup)) {
                rules.CoinsForPowerup = CoinsForPowerup;
            }
            if (rulesChanges.HasFlag(Rules.Lives)) {
                rules.Lives = Lives;
            }
            if (rulesChanges.HasFlag(Rules.TimerMinutes)) {
                rules.TimerMinutes = TimerMinutes;
            }
            if (rulesChanges.HasFlag(Rules.TeamsEnabled)) {
                rules.TeamsEnabled = TeamsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.CustomPowerupsEnabled)) {
                rules.CustomPowerupsEnabled = CustomPowerupsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.StarFountain)) {
                rules.StarFountain = StarFountain;
            }
            if (rulesChanges.HasFlag(Rules.StarCoinsEnabled)) {
                rules.StarCoinsEnabled = StarCoinsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.CharacterSpecialPhysicsEnabled)) {
                rules.CharacterSpecialPhysicsEnabled = CharacterSpecialPhysicsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.StageChooseMode)) {
                rules.ChooseMode = ChooseMode;
            }
            if (rulesChanges.HasFlag(Rules.TeamAttack)) {
                rules.TeamAttack = (TeamAttackOptions) TeamAttack;
            }

            f.Global->Rules = rules;
            f.Events.RulesChanged(gamemodeChanged, levelChanged);
        }

        [Flags]
        public enum Rules : ushort {
            None = 0,
            Stage = 1 << 0,
            Gamemode = 1 << 1,
            StarsToWin = 1 << 2,
            CoinsForPowerup = 1 << 3,
            Lives = 1 << 4,
            TimerMinutes = 1 << 5,
            TeamsEnabled = 1 << 6,
            CustomPowerupsEnabled = 1 << 7,
            // 1 << 8 retired (was DrawOnTimeUp)
            StarFountain = 1 << 9, // only for Star Chasers
            StarCoinsEnabled = 1 << 10, // only for Star Chasers
            StageChooseMode = 1 << 11,
            TeamAttack = 1 << 12,
            CharacterSpecialPhysicsEnabled = 1 << 13, // only for Star Chasers
        }
    }
}
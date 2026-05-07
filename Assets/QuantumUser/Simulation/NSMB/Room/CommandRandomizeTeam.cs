using Photon.Deterministic;
using System.Collections.Generic;
using System.Linq;

namespace Quantum
{
    public class CommandRandomizeTeam : DeterministicCommand, ILobbyCommand {
        public int[] Teams;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Teams);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* senderData) {
            int teams = Teams.Length;
            // GOtta stop those filthy cheaters :/
            if (teams < 2 || teams > 5 || !senderData->IsRoomHost) {
                return;
            }

            // cannot store pointers, store entityRef to the players
            List<EntityRef> playerEntityRefs = new();
            foreach ((var entityRef, var playerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                if (!playerData->ManualSpectator) {
                    playerEntityRefs.Add(entityRef);
                }
            }

            // now handle the list, this alGOrithm prevents infinite loops!
            int loopCount = playerEntityRefs.Count;
            for (int i = 0; i < loopCount; i++) {
                int team = Teams[i % teams];
                int index = f.RNG->Next(0, playerEntityRefs.Count);

                var playerData = f.Unsafe.GetPointer<PlayerData>(playerEntityRefs[index]);
                playerData->RequestedTeam = (byte) team;
                playerData->IsTeamLocked = true;
                playerEntityRefs.RemoveAt(index);
                f.Events.PlayerDataChanged(playerData->PlayerRef);
                f.Events.PlayerTeamRandomized(playerData->PlayerRef, (byte) team);
            }
        }
    }
}

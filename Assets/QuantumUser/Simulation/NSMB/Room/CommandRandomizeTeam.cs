using Photon.Deterministic;
using System.Collections.Generic;
using System.Linq;

namespace Quantum
{
    public class CommandRandomizeTeam : DeterministicCommand, ILobbyCommand {
        public byte[] Teams;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Teams);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* senderData) {
            int teams = Teams.Length;
            if (teams < 2 || teams > 5 || f.Global->Host != sender) {
                return;
            }

            // Count the number of players
            int numPlayers = 0;
            foreach ((_, var playerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                if (!playerData->ManualSpectator) {
                    numPlayers++;
                }
            }

            // Generate the possible team assignments
            int assignmentsPerTeam = numPlayers / teams;
            List<byte> teamAssignments = new();
            foreach (int team in Enumerable.Range(0, 5).Take(teams)) {
                for (int i = 0; i < assignmentsPerTeam; i++) {
                    teamAssignments.Add((byte)team);
                }
            }

            foreach ((_, var data) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                // Choose a random team the list
                if (teamAssignments.Count > 0) {
                    int index = f.RNG->Next(0, teamAssignments.Count);
                    data->RequestedTeam = teamAssignments[index];
                    teamAssignments.RemoveAt(index);
                } else {
                    data->RequestedTeam = (byte) f.RNG->Next(0, teams);
                }
                data->IsTeamLocked = true;
            }
        }
    }
}

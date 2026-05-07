using Quantum;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class TeamRandButton : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private Sprite overlayUnpressed, overlayPressed;
        [SerializeField] private Image icon;
        [SerializeField] public int teamCount;

        public unsafe void OnEnable() {
            var game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

            var teams = f.Context.GetAllAssets<TeamAsset>();
        }

        public void OnDisable() {

        }
    }
}

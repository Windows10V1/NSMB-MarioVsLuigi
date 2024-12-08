using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus {
    public class PlayerListPanel : InRoomPanel {

        //---Properties
        public override GameObject DefaultSelectedObject => playerList.GetPlayerEntryAtIndex(0).button.gameObject;

        //---Serialized Variables
        [SerializeField] private PlayerListHandler playerList;

        //---Private Variables
        private int selectedIndex;

        public void OnEnable() {
            playerList.PlayerRemoved += OnPlayerRemoved;
            PlayerListEntry.PlayerEntrySelected += OnPlayerEntrySelected;
        }

        public void OnDisable() {
            playerList.PlayerRemoved -= OnPlayerRemoved;
            PlayerListEntry.PlayerEntrySelected -= OnPlayerEntrySelected;
        }

        public override void Select(bool setDefault) {
            base.Select(setDefault);
            selectedIndex = 0;
        }

        public override void Deselect() {
            selectedIndex = -1;
        }

        private void OnPlayerRemoved(int index) {
            if (selectedIndex == index) {
                PlayerListEntry next = playerList.GetPlayerEntryAtIndex(selectedIndex);
                if (next.player == PlayerRef.None) {
                    selectedIndex--;
                    next = playerList.GetPlayerEntryAtIndex(selectedIndex);
                }

                EventSystem.current.SetSelectedGameObject(next.button.gameObject);
            }
        }

        private void OnPlayerEntrySelected(PlayerListEntry entry) {
            menu.SelectPanel(this, false);
            selectedIndex = playerList.GetPlayerEntryIndexOf(entry);
        }
    }
}

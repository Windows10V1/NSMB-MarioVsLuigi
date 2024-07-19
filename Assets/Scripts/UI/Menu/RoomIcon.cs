﻿using NSMB.Translation;
using NSMB.Utils;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NSMB.UI.MainMenu {
    public class RoomIcon : MonoBehaviour {

        //---Public Variables
        public RoomInfo room;

        //---Serialized Variables
        [SerializeField] private Color defaultColor, highlightColor, selectedColor;
        [SerializeField] private TMP_Text playersText, nameText, inProgressText, symbolsText;

        //---Private Variables
        private Image icon;

        public void Start() {
            icon = GetComponent<Image>();
            Unselect();
        }

        public void UpdateUI(RoomInfo newRoomInfo) {
            room = newRoomInfo;

            TranslationManager tm = GlobalController.Instance.translationManager;

            string host = newRoomInfo.CustomProperties[Enums.NetRoomProperties.HostName] as string;
            nameText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", host.ToValidUsername());

            /*
            NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string hostname);
            NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.IntProperties, out int packedIntProperties);
            NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.BoolProperties, out int packedBoolProperties);

            NetworkUtils.IntegerProperties intProperties = (NetworkUtils.IntegerProperties) packedIntProperties;
            NetworkUtils.BooleanProperties boolProperties = (NetworkUtils.BooleanProperties) packedBoolProperties;

            playersText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.players", "players", session.PlayerCount.ToString(), "maxplayers", intProperties.maxPlayers.ToString());
            inProgressText.text = boolProperties.gameStarted ? tm.GetTranslation("ui.rooms.listing.status.started") : tm.GetTranslation("ui.rooms.listing.status.notstarted");

            string symbols = "";

            if (boolProperties.customPowerups) {
                symbols += "<sprite name=room_powerups>";
            }

            if (boolProperties.teams) {
                symbols += "<sprite name=room_teams>";
            }

            if (intProperties.timer > 0) {
                symbols += "<sprite name=room_timer>" + Utils.Utils.GetSymbolString(intProperties.timer.ToString(), Utils.Utils.smallSymbols);
            }

            if (intProperties.lives > 0) {
                symbols += "<sprite name=room_lives>" + Utils.Utils.GetSymbolString(intProperties.lives.ToString(), Utils.Utils.smallSymbols);
            }

            symbols += "<sprite name=room_stars>" + Utils.Utils.GetSymbolString(intProperties.starRequirement.ToString(), Utils.Utils.smallSymbols);
            symbols += "<sprite name=room_coins>" + Utils.Utils.GetSymbolString(intProperties.coinRequirement.ToString(), Utils.Utils.smallSymbols);

            symbolsText.text = symbols;
            */
        }

        public void Select() {
            icon.color = selectedColor;
        }

        public void Unselect() {
            icon.color = defaultColor;
        }

        public void Hover() {
            icon.color = highlightColor;
        }

        public void Unhover() {
            if (MainMenuManager.Instance.roomManager.SelectedRoomIcon == this) {
                Select();
            } else {
                Unselect();
            }
        }
    }
}

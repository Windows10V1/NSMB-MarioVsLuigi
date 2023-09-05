using UnityEngine;
using UnityEngine.UI;
using TMPro;

using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

public class UserNametag : MonoBehaviour {

    public PlayerController parent;
    private PlayerData data;
    private CharacterData character;

    //---Serailzied Variables
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject nametag;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Image arrow;
    [SerializeField] private RectTransform parentTransform;

    //---Private Variables
    private string cachedNickname;
    private NicknameColor nicknameColor;

    public void Start() {
        data = parent.Object.InputAuthority.GetPlayerData(parent.Runner);
        character = data.GetCharacterData();
        arrow.color = parent.animationController.GlowColor;

        nicknameColor = data.NicknameColor;
        text.color = nicknameColor.color;
    }

    public void LateUpdate() {

        if (!parent || parent.Disconnected || !data || !data.Object) {
            Destroy(gameObject);
            return;
        }

        nametag.SetActive(!(parent.IsDead && parent.IsRespawning) && GameData.Instance.GameState >= Enums.GameState.Playing);

        if (!nametag.activeSelf)
            return;

        Vector2 worldPos = parent.animationController.models.transform.position;
        worldPos.y += parent.WorldHitboxSize.y * 1.2f + 0.5f;

        if (GameManager.Instance.loopingLevel) {
            // Wrapping
            if (Mathf.Abs(worldPos.x - cam.transform.position.x) > GameManager.Instance.LevelWidth * 0.5f) {
                worldPos.x += (cam.transform.position.x > GameManager.Instance.LevelMiddleX ? 1 : -1) * GameManager.Instance.LevelWidth;
            }
        }

        transform.position = cam.WorldToViewportPoint(worldPos, Camera.MonoOrStereoscopicEye.Mono) * parentTransform.sizeDelta;
        transform.position += parentTransform.position - (Vector3) (parentTransform.pivot * parentTransform.rect.size);

        cachedNickname ??= data.GetNickname();

        // TODO: this allocates every frame.

        string newText = (data.IsRoomOwner ? "<sprite name=room_host>" : "");
        if (SessionData.Instance.Teams && Settings.Instance.GraphicsColorblind) {
            Team team = ScriptableManager.Instance.teams[data.Team];
            newText += team.textSpriteColorblindBig;
        }
        newText += cachedNickname + "\n";

        if (parent.Lives >= 0)
            newText += character.uistring + Utils.GetSymbolString("x" + parent.Lives + " ");

        newText += Utils.GetSymbolString("Sx" + parent.Stars);

        text.text = newText;

        nicknameColor ??= data.NicknameColor;
        if (nicknameColor != null && nicknameColor.isRainbow)
            text.color = Utils.GetRainbowColor(parent.Runner);
    }
}

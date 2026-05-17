using NSMB.UI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ChangeTextOnSelect : MonoBehaviour, ISelectHandler {

        //---Serialized Variables
        [SerializeField] protected TMP_Text text;
        [SerializeField] protected string translationKey;

        public virtual void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public virtual void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void ApplyText() {
            text.text = GetText();
        }

        public virtual string GetText() {
            return GlobalController.Instance.translationManager.GetTranslation(translationKey);
        }

        void ISelectHandler.OnSelect(BaseEventData eventData) {
            ApplyText();
        }

        private void OnLanguageChanged(TranslationManager tm) {
            ApplyText();
        }
    }
}
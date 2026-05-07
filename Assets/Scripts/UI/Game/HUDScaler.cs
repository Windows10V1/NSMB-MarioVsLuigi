using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game {
    public class HUDScaler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private Vector2 baseline = new Vector2(1000f, 562.5f);
        [SerializeField] private float pxPerStep = 150;

        public void OnValidate() {
            this.SetIfNull(ref scaler);
        }

        public void OnEnable() {
            Settings.OnHudScaleChanged += OnHudScaleChanged;
            OnHudScaleChanged(Settings.Instance.GraphicsHudScale);
        }

        public void OnDisable() {
            Settings.OnHudScaleChanged -= OnHudScaleChanged;
        }

        private void OnHudScaleChanged(float value) {
            Vector2 newScale = baseline;
            newScale.y += pxPerStep * (8 - value);
            scaler.referenceResolution = newScale;
        }
    }
}
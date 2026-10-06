using NSMB.Sound;
using NSMB.Utilities.Components;
using Quantum;
using System;
using UnityEngine;

namespace NSMB.Quantum {
    public class StageContext : QuantumMonoBehaviour, IQuantumViewContext {

        public QuantumMapData MapData;
        [NonSerialized] public VersusStageData Stage;

        public void Awake() {
            Stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(MapData.GetAsset(false).UserAsset);
            SoundEffectResolver.Instance.GlobalProviders.Add(Stage);

            // Static level lights (e.g. custom stages) need wrap ghosts so point
            // lights next to the loop-point also illuminate the other side.
            if (Stage != null && Stage.IsWrappingLevel) {
                foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                    if (light.type != LightType.Point && light.type != LightType.Spot) {
                        continue;
                    }
                    if (light.GetComponent<WrappingLight>() || light.gameObject.name.Contains("(Wrap ")) {
                        continue;
                    }
                    light.gameObject.AddComponent<WrappingLight>();
                }
            }
        }

        public void OnDestroy() {
            if (SoundEffectResolver.Instance) {
                SoundEffectResolver.Instance.GlobalProviders.Remove(Stage);
            }
        }
    }
}

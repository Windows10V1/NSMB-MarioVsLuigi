using NSMB.Utilities.Extensions;
using Quantum;
using System;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public unsafe class StarCoinAnimator : QuantumEntityViewComponent {

        //---Static
        public static event Action<Frame, StarCoinAnimator> StarCoinInitialized;
        public static event Action<Frame, StarCoinAnimator> StarCoinDestroyed;

        //---Serialized Variables
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private MeshRenderer mRenderer;
        [SerializeField] private GameObject starCoinCollectPrefab;

        //---Private Variables
        private bool collected;

        public void OnValidate() {
            this.SetIfNull(ref animator);
            this.SetIfNull(ref sfx);
            this.SetIfNull(ref mRenderer, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            EntityView.OnEntityDestroyed.AddListener(OnEntityDestroyed);
        }

        public override unsafe void OnActivate(Frame f) {
            if (f.Global->GameState == GameState.Playing && !IsReplayFastForwarding) {
                sfx.PlayOneShot(SoundEffect.World_Star_Spawn);
            }
            StarCoinInitialized?.Invoke(f, this);
        }

        public void OnDestroy() {
            EntityView.OnEntityDestroyed.RemoveListener(OnEntityDestroyed);
        }

        public override void OnUpdateView() {
            animator.enabled = PredictedFrame.Global->GameState < GameState.Ended;
        }

        public void OnEntityDestroyed(QuantumGame game) {
            if (!IsReplayFastForwarding && collected) {
                sfx.PlayOneShot(SoundEffect.World_Starcoin_Store);
            }
            mRenderer.enabled = false;
            Destroy(gameObject, 2);
            StarCoinDestroyed?.Invoke(VerifiedFrame, this);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            collected = false;
        }
    }
}

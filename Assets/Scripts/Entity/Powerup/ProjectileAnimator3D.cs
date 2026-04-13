using NSMB.Quantum;
using NSMB.UI.Game;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Rendering;

namespace NSMB.Entities.Powerup {
    public unsafe class ProjectileAnimator3D : QuantumEntityViewComponent {

        private static readonly int ParamOverallsColor = Shader.PropertyToID("OverallsColor");
        private static readonly int ParamShirtColor = Shader.PropertyToID("ShirtColor");
        private static readonly int ParamHatUsesOverallsColor = Shader.PropertyToID("HatUsesOverallsColor");
        private static readonly int ParamGlowColor = Shader.PropertyToID("GlowColor");
        private static readonly int ParamMultiplyColor = Shader.PropertyToID("MultiplyColor");

        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private bool usePalette = true;
        [SerializeField] private Color sameTeamColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color differentTeamColor = Color.white;

        private EntityRef owner;
        private MaterialPropertyBlock materialBlock;
        private CharacterSpecificPalette skin;
        private Color glowColor;

        public void OnValidate() {
            this.SetIfNull(ref meshRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
        }

        public override unsafe void OnActivate(Frame f) {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            
            var projectile = f.Unsafe.GetPointer<Projectile>(EntityRef);
            owner = projectile->Owner;

            if (projectile->FacingRight) {
                if (animator) {
                    animator.Play("Left");
                }
            }

            if (usePalette && f.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
                var playerData = QuantumUtils.GetPlayerData(f, ownerMario->PlayerRef);
                if (playerData != null && f.TryFindAsset(playerData->Palette, out var palette)) {
                    if (f.TryFindAsset(ownerMario->CharacterAsset, out var characterAsset)) {
                        skin = palette.GetPaletteForCharacter(characterAsset);
                    }
                }
                glowColor = Utils.GetPlayerColor(f, ownerMario->PlayerRef);
            }

            TryCreateMaterialBlock();
        }

        public override void OnDeactivate() {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        public override unsafe void OnUpdateView() {
            if (PredictedFrame.Unsafe.TryGetPointer(EntityRef, out Projectile* projectile)) {
                EntityRef newOwner = projectile->Owner;
                
                if (newOwner != owner) {
                    owner = newOwner;
                    
                    if (usePalette && PredictedFrame.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
                        var playerData = QuantumUtils.GetPlayerData(PredictedFrame, ownerMario->PlayerRef);
                        if (playerData != null && PredictedFrame.TryFindAsset(playerData->Palette, out var palette)) {
                            if (PredictedFrame.TryFindAsset(ownerMario->CharacterAsset, out var characterAsset)) {
                                skin = palette.GetPaletteForCharacter(characterAsset);
                            }
                        }
                        glowColor = Utils.GetPlayerColor(PredictedFrame, ownerMario->PlayerRef);
                    }
                }
            }
        }

        private void TryCreateMaterialBlock() {
            if (materialBlock != null) {
                return;
            }

            materialBlock = new MaterialPropertyBlock();

            if (usePalette) {
                materialBlock.SetVector(ParamOverallsColor, skin?.OverallsColor.AsColor.linear ?? Color.clear);
                materialBlock.SetVector(ParamShirtColor, skin?.ShirtColor != null ? skin.ShirtColor.AsColor.linear : Color.clear);
                materialBlock.SetFloat(ParamHatUsesOverallsColor, (skin?.HatUsesOverallsColor ?? false) ? 1 : 0);
            }

            if (meshRenderer != null) {
                meshRenderer.SetPropertyBlock(materialBlock);
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) {
            if (materialBlock == null || meshRenderer == null) {
                return;
            }

            bool teams = PredictedFrame.Global->Rules.TeamsEnabled;
            materialBlock.SetColor(ParamGlowColor, teams || !IsCameraFocus(camera) ? glowColor : Color.clear);

            materialBlock.SetColor(ParamMultiplyColor, IsCameraTeamFocus(camera) ? sameTeamColor : differentTeamColor);

            meshRenderer.SetPropertyBlock(materialBlock);
        }

        private bool IsCameraFocus(Camera camera) {
            foreach (var playerElement in PlayerElements.AllPlayerElements) {
                if (owner == playerElement.Entity && playerElement.IsOurCamera(camera)) {
                    return true;
                }
            }
            return false;
        }

        private unsafe bool IsCameraTeamFocus(Camera camera) {
            if (!PredictedFrame.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
                return false;
            }

            foreach (var playerElement in PlayerElements.AllPlayerElements) {
                if (playerElement.IsOurCamera(camera)) {
                    if (!PredictedFrame.Unsafe.TryGetPointer(playerElement.Entity, out MarioPlayer* cameraMario)) {
                        return false;
                    }

                    return cameraMario->GetTeam(PredictedFrame) == ownerMario->GetTeam(PredictedFrame);
                }
            }
            return false;
        }
    }
}

using NSMB.Quantum;
using NSMB.UI.Game;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Rendering;

namespace NSMB.Entities.Powerup {
    public unsafe class ProjectileAnimator3D : QuantumEntityViewComponent {

        //---Static Variables
        private static readonly int ParamOverallsColor = Shader.PropertyToID("OverallsColor");
        private static readonly int ParamShirtColor = Shader.PropertyToID("ShirtColor");
        private static readonly int ParamHatUsesOverallsColor = Shader.PropertyToID("HatUsesOverallsColor");
        private static readonly int ParamGlowColor = Shader.PropertyToID("GlowColor");
        private static readonly int ParamMultiplyColor = Shader.PropertyToID("MultiplyColor");

        //---Serialized Variables
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private bool usePalette = true;
        [SerializeField] private Color sameTeamTint = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color differentTeamTint = Color.white;

        //---Private Variables
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

            // Handle animation direction based on facing
            if (projectile->FacingRight) {
                if (animator) {
                    animator.Play("Left");
                }
            }

            // Get the owner player's palette data (only if usePalette is enabled)
            if (usePalette && f.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
                var playerData = QuantumUtils.GetPlayerData(f, ownerMario->PlayerRef);
                if (playerData != null && f.TryFindAsset(playerData->Palette, out var palette)) {
                    // Get the character asset from the owner to find the right palette
                    if (f.TryFindAsset(ownerMario->CharacterAsset, out var characterAsset)) {
                        skin = palette.GetPaletteForCharacter(characterAsset);
                    }
                }
                glowColor = Utils.GetPlayerColor(f, ownerMario->PlayerRef);
            }

            // Initialize the material property block with palette colors
            TryCreateMaterialBlock();
        }

        public override void OnDeactivate() {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        public override unsafe void OnUpdateView() {
            // Update owner reference in case of entity ref hijacking
            if (PredictedFrame.Unsafe.TryGetPointer(EntityRef, out Projectile* projectile)) {
                owner = projectile->Owner;
            }
        }

        private void TryCreateMaterialBlock() {
            if (materialBlock != null) {
                return;
            }

            materialBlock = new MaterialPropertyBlock();

            // Apply palette colors to the material property block (only if enabled)
            if (usePalette) {
                materialBlock.SetVector(ParamOverallsColor, skin?.OverallsColor.AsColor.linear ?? Color.clear);
                materialBlock.SetVector(ParamShirtColor, skin?.ShirtColor != null ? skin.ShirtColor.AsColor.linear : Color.clear);
                materialBlock.SetFloat(ParamHatUsesOverallsColor, (skin?.HatUsesOverallsColor ?? false) ? 1 : 0);
            }

            // Apply the property block to the renderer
            if (meshRenderer != null) {
                meshRenderer.SetPropertyBlock(materialBlock);
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) {
            if (materialBlock == null || meshRenderer == null) {
                return;
            }

            // Update glow color based on camera team focus (similar to player shader logic)
            bool teams = PredictedFrame.Global->Rules.TeamsEnabled;
            materialBlock.SetColor(ParamGlowColor, teams || !IsCameraFocus(camera) ? glowColor : Color.clear);

            // Apply team-based tint (darker if same team, normal if different team)
            materialBlock.SetColor(ParamMultiplyColor, IsCameraTeamFocus(camera) ? sameTeamTint : differentTeamTint);

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
                    // This camera.
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

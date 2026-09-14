using NSMB.Entities.Player;
using NSMB.UI.Game;
using NSMB.Utilities.Components;
using NSMB.Utilities.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NSMB.Entities.CoinItems {
    public class ProjectileAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [Header("Sprite")]
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private LegacyAnimateSpriteRenderer legacySpriteAnimator;
        [SerializeField] private Color sameTeamColor, differentTeamColor;

        [Header("Model")]
        [SerializeField] private Renderer[] modelRenderers;

        [Header("Palette")]
        [SerializeField] private bool usePalette;
        [SerializeField] private PowerupVisuals.MaterialTextureReplacement[] paletteReplacements;

        //---Shader Properties
        private static readonly int ParamMultiplyColor = Shader.PropertyToID("_MultiplyColor");
        private static readonly int ParamBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ParamOverallsColor = Shader.PropertyToID("_OverallsColor");
        private static readonly int ParamShirtColor = Shader.PropertyToID("_ShirtColor");
        private static readonly int ParamCapUsesOverallsColor = Shader.PropertyToID("_CapUsesOverallsColor");
        private static readonly int ParamMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int ParamOverallsMask = Shader.PropertyToID("_OverallsMask");
        private static readonly int ParamShirtMask = Shader.PropertyToID("_ShirtMask");
        private static readonly int ParamCapMask = Shader.PropertyToID("_CapMask");

        //---Private Variables
        private EntityRef owner;
        private CharacterSpecificPalette skin;
        private MaterialPropertyBlock materialBlock;
        private readonly Dictionary<Material, Material> clonedMaterials = new();

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref legacySpriteAnimator, UnityExtensions.GetComponentType.Children);
            RefreshModelRenderers();
        }

        private void RefreshModelRenderers() {
            if (modelRenderers == null || modelRenderers.Length == 0) {
                List<Renderer> renderers = new();
                renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
                renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
                modelRenderers = renderers.ToArray();
            }
        }

        public void Awake() {
            RefreshModelRenderers();
            if (!usePalette || modelRenderers == null || modelRenderers.Length == 0) {
                return;
            }

            // Get copies of all materials so palette textures don't touch shared assets.
            foreach (Renderer r in modelRenderers) {
                List<Material> sharedMaterials = new();
                r.GetSharedMaterials(sharedMaterials);
                for (int i = 0; i < sharedMaterials.Count; i++) {
                    Material material = sharedMaterials[i];
                    if (!clonedMaterials.TryGetValue(material, out Material clonedMaterial)) {
                        clonedMaterials[material] = clonedMaterial = Instantiate(material);
                    }
                    sharedMaterials[i] = clonedMaterial;
                }
                r.SetSharedMaterials(sharedMaterials);
            }

            if (paletteReplacements != null) {
                foreach (var replacement in paletteReplacements) {
                    if (replacement != null && replacement.Material && clonedMaterials.TryGetValue(replacement.Material, out Material cloned)) {
                        replacement.Material = cloned;
                    }
                }
                ApplyPaletteTextures();
            }
        }

        private void ApplyPaletteTextures() {
            if (paletteReplacements == null) {
                return;
            }

            foreach (var replacement in paletteReplacements) {
                if (replacement == null || !replacement.Material) {
                    continue;
                }
                replacement.Material.SetTexture(ParamMainTex, replacement.AlbedoTexture);
                replacement.Material.SetTexture(ParamOverallsMask, replacement.OverallsMaskTexture);
                replacement.Material.SetTexture(ParamShirtMask, replacement.ShirtMaskTexture);
                replacement.Material.SetTexture(ParamCapMask, replacement.CapMaskTexture);
            }
        }

        public override unsafe void OnActivate(Frame f) {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RefreshModelRenderers();
            var projectile = f.Unsafe.GetPointer<Projectile>(EntityRef);

            owner = projectile->Owner;
            ResolveSkin(f);
            ResetVisuals();

            if (projectile->FacingRight) {
                if (sRenderer) {
                    sRenderer.flipX = true;
                }
                if (animator) {
                    animator.Play("Left");
                }
            }
        }

        public override unsafe void OnUpdateView() {
            if (PredictedFrame.Unsafe.TryGetPointer(EntityRef, out Projectile* projectile)) {
                // Fixes EntityRef hijacking so pooled projectiles don't keep the previous owner's colors.
                if (owner != projectile->Owner) {
                    owner = projectile->Owner;
                    ResolveSkin(PredictedFrame);
                }
            }

            if (animator) {
                animator.enabled = PredictedFrame.Global->GameState == GameState.Playing;
            }
            if (legacySpriteAnimator) {
                legacySpriteAnimator.enabled = PredictedFrame.Global->GameState == GameState.Playing;
            }
        }

        public override void OnDeactivate() {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            ResetVisuals();
        }

        // Clears the team tints on both the sprite and the models so recycled (pooled)
        // projectiles don't briefly render with the previous owner's colors.
        private void ResetVisuals() {
            if (sRenderer) {
                sRenderer.color = Color.white;
            }
            if (modelRenderers != null) {
                foreach (Renderer r in modelRenderers) {
                    if (r) {
                        r.SetPropertyBlock(null);
                    }
                }
            }
            materialBlock = null;
        }

        private unsafe void ResolveSkin(Frame f) {
            skin = null;
            if (!f.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario) || !ownerMario->PlayerRef.IsValid) {
                return;
            }

            PlayerData* playerData = QuantumUtils.GetPlayerData(f, ownerMario->PlayerRef);
            if (playerData == null || !f.TryFindAsset(playerData->Palette, out PaletteSet palette)) {
                return;
            }

            skin = palette.GetPaletteForCharacter(playerData->Character);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext src, Camera camera) {
            /* Try/Catch is a bodge for this error:
                Render Pipeline error : the XR layout still contains active passes. Executing XRSystem.EndLayout() right now.
                NullReferenceException
                  at (wrapper managed-to-native) UnityEngine.SpriteRenderer.set_color_Injected(UnityEngine.SpriteRenderer,UnityEngine.Color&)
                  at UnityEngine.SpriteRenderer.set_color (UnityEngine.Color value) [0x00000] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at ProjectileAnimator.OnBeginCameraRendering (UnityEngine.Rendering.ScriptableRenderContext src, UnityEngine.Camera camera) [0x0000d] in <e9b2d65d314645db895f8bc71e0abf60>:0 
                  at UnityEngine.Rendering.RenderPipelineManager.BeginCameraRendering (UnityEngine.Rendering.ScriptableRenderContext context, UnityEngine.Camera camera) [0x0000a] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at UnityEngine.Rendering.RenderPipeline.BeginCameraRendering (UnityEngine.Rendering.ScriptableRenderContext context, UnityEngine.Camera camera) [0x00001] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at UnityEngine.Rendering.Universal.UniversalRenderPipeline.RenderCameraStack (UnityEngine.Rendering.ScriptableRenderContext context, UnityEngine.Camera baseCamera) [0x002ba] in <26b2602f421d48c299968e0ff9498adf>:0 
                  at UnityEngine.Rendering.Universal.UniversalRenderPipeline.Render (UnityEngine.Rendering.ScriptableRenderContext renderContext, System.Collections.Generic.List`1[T] cameras) [0x0009b] in <26b2602f421d48c299968e0ff9498adf>:0 
                  at UnityEngine.Rendering.RenderPipeline.InternalRender (UnityEngine.Rendering.ScriptableRenderContext context, System.Collections.Generic.List`1[T] cameras) [0x0001c] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at UnityEngine.Rendering.RenderPipelineManager.DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset pipe, System.IntPtr loopPtr, UnityEngine.Object renderRequest) [0x00046] in <935634f5cc14479dbaa30641d55600a9>:0 
            */
            try {
                if (sRenderer) {
                    sRenderer.color = IsCameraTeamFocus(camera) ? sameTeamColor : differentTeamColor;
                }
                if (modelRenderers != null && modelRenderers.Length > 0) {
                    ApplyModelMaterials(camera);
                }
            } catch {
                // Debug.LogWarning("The bug happened");
            }
        }

        private void ApplyModelMaterials(Camera camera) {
            Color tint = IsCameraTeamFocus(camera) ? sameTeamColor : differentTeamColor;

            materialBlock ??= new MaterialPropertyBlock();
            materialBlock.SetColor(ParamMultiplyColor, tint);
            materialBlock.SetColor(ParamBaseColor, tint);

            if (usePalette && skin != null) {
                materialBlock.SetColor(ParamOverallsColor, skin.OverallsColor.AsColor);
                materialBlock.SetColor(ParamShirtColor, skin.ShirtColor.AsColor);
                materialBlock.SetFloat(ParamCapUsesOverallsColor, skin.HatUsesOverallsColor ? 1 : 0);
            }

            foreach (Renderer r in modelRenderers) {
                if (r) {
                    r.SetPropertyBlock(materialBlock);
                }
            }
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
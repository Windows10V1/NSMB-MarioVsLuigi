using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Game;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "PowerupTile", menuName = "ScriptableObjects/Tiles/PowerupTile")]
    public class PowerupTile : BreakableBrickTile, IHaveTileDependencies {

        //---Static Variables
        private static readonly Vector2 SpawnOffset = new(0, -0.25f);

        //---Serialized Variables
        [SerializeField] private TileBase resultTile;
        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            if (base.Interact(interacter, direction, worldLocation, out bumpSound))
                return true;

            bumpSound = true;

            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);

            NetworkPrefabRef spawnResult = PrefabList.Instance.Powerup_Mushroom;

            if ((interacter is PlayerController) || (interacter is KoopaWalk koopa && koopa.PreviousHolder != null)) {
                PlayerController player = interacter is PlayerController controller ? controller : ((KoopaWalk) interacter).PreviousHolder;
                if (player.State == Enums.PowerupState.MegaMushroom) {
                    //Break

                    //Tilemap
                    GameManager.Instance.tileManager.SetTile(tileLocation, null);

                    //Particle
                    //TODO:
                    GameManager.Instance.particleManager.Play(Enums.Particle.Entity_BrickBreak, Utils.Utils.TilemapToWorldPosition(tileLocation) + Vector3.one * 0.25f, particleColor);

                    interacter.PlaySound(Enums.Sounds.World_Block_Break);
                    return true;
                }

                if (player.State > Enums.PowerupState.MiniMushroom)
                    spawnResult = PrefabList.Instance.Powerup_FireFlower;
            }

            Bump(interacter, direction, worldLocation);
            bool downwards = direction == InteractionDirection.Down;
            GameData.Instance.BumpBlock((short) tileLocation.x, (short) tileLocation.y, this,
                resultTile, downwards, SpawnOffset, false, spawnResult);
            return false;
        }

        public TileBase[] GetTileDependencies() {
            return new TileBase[] { resultTile };
        }
    }
}

using Photon.Deterministic;

namespace Quantum {
    /// <summary>
    /// System for handling Goldball projectile's brick-to-coin conversion.
    /// When a Goldball hits any surface, it converts all breakable bricks within a radius into stage coins.
    /// </summary>
    public unsafe class GoldballSystem {

        // Maximum lifetime for coins created by Goldball (effectively infinite at 60 FPS = ~18 minutes)
        private const ushort COIN_INFINITE_LIFETIME = ushort.MaxValue;

        /// <summary>
        /// Converts all breakable bricks within radius of the hit position into stage coins.
        /// Called when a Goldball projectile hits any surface.
        /// </summary>
        public static void TriggerGoldballEffect(Frame f, EntityRef projectileEntity, FPVector2 hitPosition, ProjectileAsset asset) {
            if (!asset.IsGoldball) {
                return;
            }

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            FP radius = FP._2;
            FP radiusSquared = radius * radius;

            // Scan tiles within the radius
            int tileRadius = FPMath.CeilToInt(radius);
            IntVector2 centerTile = QuantumUtils.WorldToRelativeTile(f, hitPosition);

            int bricksConverted = 0;
            int minX = centerTile.X - tileRadius;
            int maxX = centerTile.X + tileRadius;
            int minY = centerTile.Y - tileRadius;
            int maxY = centerTile.Y + tileRadius;

            for (int x = minX; x <= maxX; x++) {
                for (int y = minY; y <= maxY; y++) {
                    IntVector2 tilePos = new IntVector2(x, y);
                    FPVector2 tileWorldPos = QuantumUtils.RelativeTileToWorldRounded(stage, tilePos);
                    
                    // Check if tile is within circular radius
                    FPVector2 delta = tileWorldPos - hitPosition;
                    if (delta.SqrMagnitude > radiusSquared) {
                        continue;
                    }

                    // Check if this tile is a breakable brick that allows Goldball conversion
                    if (TryConvertBrickToCoin(f, stage, tilePos)) {
                        bricksConverted++;
                    }
                }
            }

            // Destroy the Goldball projectile with particle effect
            ProjectileSystem.Destroy(f, projectileEntity, asset.DestroyParticleEffect);
        }

        /// <summary>
        /// Attempts to convert a single brick at the given tile position to a stage coin.
        /// Returns true if conversion was successful.
        /// </summary>
        private static bool TryConvertBrickToCoin(Frame f, VersusStageData stage, IntVector2 tilePos) {
            StageTileInstance tileInstance = stage.GetTileRelative(f, tilePos);
            if (!tileInstance.Tile.IsValid) {
                return false;
            }

            StageTile tile = f.FindAsset(tileInstance.Tile);
            if (tile is not BreakableBrickTile breakableBrick) {
                return false;
            }

            // Only convert if the brick allows Goldball breaking
            if (!breakableBrick.BreakingRules.HasFlag(BreakableBrickTile.BreakableBy.Goldballs)) {
                return false;
            }

            // Create stage coin at brick position
            FPVector2 coinPos = QuantumUtils.RelativeTileToWorldRounded(stage, tilePos);
            EntityRef coinEntity = f.Create(f.SimulationConfig.StageCoinPrototype);
            
            // Configure the coin
            var coin = f.Unsafe.GetPointer<Coin>(coinEntity);
            coin->CoinType = (CoinType)0; // Not BakedInStage - won't respawn on reset
            coin->Lifetime = COIN_INFINITE_LIFETIME; // Effectively infinite lifetime (~18 min at 60 FPS)
            coin->UncollectableFrames = 0;
            
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
            coinTransform->Position = coinPos;

            // Remove the brick tile
            stage.SetTileRelative(f, tilePos, default);

            return true;
        }
    }
}

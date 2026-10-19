using Photon.Deterministic;
using Quantum.Physics2D;

namespace Quantum {
    public unsafe class StarCoinSystem : SystemMainThread {

        public override bool StartEnabled => false;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public StarCoin* StarCoin;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<StarCoin, MarioPlayer>(f, OnStarCoinMarioInteraction);
        }

        public override void Update(Frame f) {
            if (f.Global->Rules.StarCoinsEnabled && !f.Exists(f.Global->MainStarCoin) && QuantumUtils.Decrement(ref f.Global->StarCoinSpawnTimer)) {
                VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                HandleSpawningNewStarCoin(f, stage);
            }

            Filter filter = default;
            var filterStruct = f.Unsafe.FilterStruct<Filter>();
            while (filterStruct.Next(&filter)) {
                var starCoin = filter.StarCoin;

                if (starCoin->DespawnCounter > 0) {
                    if (QuantumUtils.Decrement(ref starCoin->DespawnCounter)) {
                        f.Events.CollectableDespawned(filter.Entity, filter.Transform->Position + (FPVector2.Down / 4), false);
                        f.Destroy(filter.Entity);
                    }
                }
            }
        }

        private void HandleSpawningNewStarCoin(Frame f, VersusStageData stage) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as StarChasersGamemode;
            if (gamemode == null) {
                return;
            }

            AssetRef<EntityPrototype> prototype = gamemode.StarCoinPrototype;
            if (!prototype.IsValid) {
                // StarCoin prototype not assigned- retry later.
                f.Global->StarCoinSpawnTimer = 30;
                return;
            }

            var spawnpoints = stage.BigStarSpawnpoints;
            int numSpawnpoints = spawnpoints.Length;
            ref BitSet64 usedSpawnpoints = ref f.Global->UsedStarSpawns;

            // Get current Big Star position to avoid spawning in same spot
            FPVector2? bigStarPosition = null;
            if (f.Exists(f.Global->MainBigStar) && f.Unsafe.TryGetPointer(f.Global->MainBigStar, out Transform2D* bigStarTransform)) {
                bigStarPosition = bigStarTransform->Position;
            }

            bool spawnedStarCoin = false;
            for (int i = 0; i < numSpawnpoints; i++) {
                // Find a spot...
                int bitsSet = usedSpawnpoints.GetSetCount();
                if (bitsSet >= numSpawnpoints) {
                    usedSpawnpoints.ClearAll();
                    bitsSet = 0;
                }

                int selection = f.RNG->Next(0, numSpawnpoints - bitsSet);
                int count = selection;
                int index = 0;
                for (int j = 0; j < numSpawnpoints; j++) {
                    if (!usedSpawnpoints.IsSet(j)) {
                        if (count-- == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
                }
                usedSpawnpoints.Set(index);

                // Spawn a starcoin.
                FPVector2 position = spawnpoints[index];

                // Avoid spawning in same spot as current Big Star
                if (bigStarPosition.HasValue) {
                    FP distanceSquared = FPVector2.DistanceSquared(position, bigStarPosition.Value);
                    if (distanceSquared < FP._0_01) {
                        // Too close to Big Star, try next spawnpoint
                        continue;
                    }
                }

                HitCollection hits = f.Physics2D.OverlapShape(position, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);
                if (hits.Count > 0) {
                    // Hit something.
                    f.Events.BigCollectableAttemptedSpawn(index, position, Success: false);
                    continue;
                }

                FP radius = f.Context.CircleRadiusTwo.BroadRadius + 1;
                if (position.X - radius <= stage.StageWorldMin.X || position.X + radius >= stage.StageWorldMax.X) {
                    // Close to the level seam... check for extra overlaps.
                    FPVector2 seamPosition = position;
                    seamPosition.X += stage.TileDimensions.X * FP._0_50 * ((position.X < stage.StageWorldMidpoint.X) ? 1 : -1);

                    HitCollection seamHits = f.Physics2D.OverlapShape(seamPosition, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);
                    if (seamHits.Count > 0) {
                        // Hit something.
                        f.Events.BigCollectableAttemptedSpawn(index, position, Success: false);
                        continue;
                    }
                }

                // Good to go.
                EntityRef newEntity = f.Create(prototype);
                f.Global->MainStarCoin = newEntity;
                f.Unsafe.GetPointer<Transform2D>(newEntity)->Position = position;
                f.Events.BigCollectableAttemptedSpawn(index, position, Success: true);
                spawnedStarCoin = true;
                break;
            }

            if (!spawnedStarCoin) {
                f.Global->StarCoinSpawnTimer = 30;
            }
        }

        public void OnStarCoinMarioInteraction(Frame f, EntityRef starCoinEntity, EntityRef marioEntity) {
            if (!f.Exists(starCoinEntity) || f.DestroyPending(starCoinEntity)) {
                return;
            }

            var starCoin = f.Unsafe.GetPointer<StarCoin>(starCoinEntity);
            if (starCoin->DespawnCounter > 0) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (mario->IsDead) {
                return;
            }

            int amountToGive = mario->Coins / 2;
            if (amountToGive == 0 && mario->Coins > 0) {
                amountToGive = 1;
            }
            CoinSystem.AddCoinsDirect(f, marioEntity, amountToGive);

            f.Global->StarCoinSpawnTimer = (ushort) ((624 - (f.Global->RealPlayers * 12)) * 2);
            f.Global->MainStarCoin = EntityRef.None;

            starCoin->DespawnCounter = 105;
            starCoin->Collector = marioEntity;
            f.Events.MarioPlayerCollectedStarCoin(marioEntity, starCoinEntity);
        }
    }
}

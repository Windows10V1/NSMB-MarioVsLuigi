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
            if (!f.Exists(f.Global->MainStarCoin) && QuantumUtils.Decrement(ref f.Global->StarCoinSpawnTimer)) {
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
            int spawnpoints = stage.BigStarSpawnpoints.Length;
            ref BitSet64 usedSpawnpoints = ref f.Global->UsedStarSpawns;

            // Get current Big Star position to avoid spawning in same spot
            FPVector2? bigStarPosition = null;
            if (f.Exists(f.Global->MainBigStar) && f.Unsafe.TryGetPointer(f.Global->MainBigStar, out Transform2D* bigStarTransform)) {
                bigStarPosition = bigStarTransform->Position;
            }

            bool spawnedStarCoin = false;
            for (int i = 0; i < spawnpoints; i++) {
                // Find a spot...
                int setBits = usedSpawnpoints.GetSetCount();
                if (setBits >= spawnpoints) {
                    usedSpawnpoints.ClearAll();
                }

                int count = f.RNG->Next(0, spawnpoints - setBits);
                int index = 0;
                for (int j = 0; j < spawnpoints; j++) {
                    if (!usedSpawnpoints.IsSet(j)) {
                        if (count-- == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
                }
                usedSpawnpoints.Set(index);

                // Spawn a coin.
                FPVector2 position = stage.BigStarSpawnpoints[index];
                
                // Avoid spawning in same spot as current Big Star
                if (bigStarPosition.HasValue) {
                    FP distanceSquared = FPVector2.DistanceSquared(position, bigStarPosition.Value);
                    if (distanceSquared < FP._0_01) {
                        continue;
                    }
                }
                
                HitCollection hits = f.Physics2D.OverlapShape(position, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);

                if (hits.Count == 0) {
                    // Hit no players
                    var gamemode = (StarChasersGamemode) f.FindAsset(f.Global->Rules.Gamemode);
                    EntityRef newEntity = f.Create(gamemode.StarCoinPrototype);
                    f.Global->MainStarCoin = newEntity;
                    var newStarCoinTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                    newStarCoinTransform->Position = position;
                    spawnedStarCoin = true;
                    f.Events.BigCollectableAttemptedSpawn(index, position, Success: true);
                    break;
                } else {
                    f.Events.BigCollectableAttemptedSpawn(index, position, Success: false);
                }
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

            // Give 4 item coins
            byte threshold = (byte) f.Global->Rules.CoinsForPowerup;
            byte oldCoins = mario->Coins;
            byte newCoins = (byte) FPMath.Min(255, mario->Coins + 4);


            int oldThresholds = oldCoins / threshold;
            int newThresholds = newCoins / threshold;
            int powerupsToSpawn = newThresholds - oldThresholds;

            EntityRef spawnedItem = EntityRef.None;
            for (int i = 0; i < powerupsToSpawn; i++) {
                spawnedItem = MarioPlayerSystem.SpawnItem(f, marioEntity, mario, default, false);
            }
            
            mario->Coins = (byte) (newCoins % threshold);
            
            if (spawnedItem.IsValid) {
                var starCoinTransform = f.Unsafe.GetPointer<Transform2D>(starCoinEntity);
                f.Events.MarioPlayerCollectedCoin(marioEntity, newCoins, spawnedItem, starCoinTransform->Position, false, false);
            }
            
            f.Global->StarCoinSpawnTimer = (ushort) ((624 - (f.Global->RealPlayers * 12)) * 2);
            f.Global->MainStarCoin = EntityRef.None;
            
            starCoin->DespawnCounter = 105;
            starCoin->Collector = marioEntity;
            f.Events.MarioPlayerCollectedStarCoin(marioEntity, starCoinEntity);
            GameLogicSystem.CheckForGameEnd(f);
        }
    }
}
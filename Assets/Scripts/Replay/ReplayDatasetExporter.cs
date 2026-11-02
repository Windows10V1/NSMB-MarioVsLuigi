using Newtonsoft.Json;
using NSMB.Replay;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace NSMB.Bots {

    /// <summary>
    /// Records a training dataset while a .mvlreplay is playing.
    /// Drop this file anywhere in the Unity project's Scripts folder. It starts itself.
    /// Play a replay from the Replays tab as usual; a CSV is written when the replay ends / you leave it.
    /// Output: [Application.persistentDataPath]/BotDataset/   (the path is printed in the Console)
    /// v3: adds lives, team, match rules, same-team flag for nearby players, and header results (winner / final stars) in the _players and _meta files.
    /// v2: adds stage name, level wrap-around distances, tile types (solid/slippery/damaging/block), underwater, alive player count.
    ///
    /// Every row = what one player could "see" on tick N-1, plus the buttons they pressed on tick N.
    /// (Quantum applies the input of tick N to the state of tick N-1, so this is the correct pairing.)
    /// </summary>
    public unsafe class ReplayDatasetExporter : MonoBehaviour {

        //---Settings (safe to edit)
        private const int TickStride = 2;        // 1 = every tick, 2 = every second tick (half the file size)
        private const int GridHalfWidth = 6;     // tile grid: 13 tiles wide...
        private const int GridHalfHeight = 4;    // ...and 9 tiles tall, centered on the player
        private const int NearestStars = 2;
        private const int NearestPlayers = 3;
        private const int NearestEnemies = 4;
        private const int NearestPowerups = 2;
        private const string Missing = "99";     // value used when there is nothing to report (e.g. no enemy nearby)

        //---Types
        private struct Obj {
            public float X, Y, VX, VY;
            public int Powerup, Stars, Id, Team;
        }

        //---Private Variables
        private readonly List<Obj> players = new();
        private readonly List<Obj> enemies = new();
        private readonly List<Obj> stars = new();
        private readonly List<Obj> powerups = new();
        private readonly bool[] used = new bool[512];

        private readonly Dictionary<int, string> pending = new();
        private readonly Dictionary<int, int> lastStars = new();
        private readonly Dictionary<int, string> names = new();
        private readonly StringBuilder sb = new(2048);

        private string stageName = "";
        private float wrapWidth;   // world width of the stage if it wraps around, otherwise 0

        private int pendingTick = int.MinValue;
        private int lastHandledTick = int.MinValue;
        private int rowCount;

        private BinaryReplayFile currentReplay;
        private StreamWriter writer;
        private string basePath;

        //---Startup
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            var go = new GameObject("ReplayDatasetExporter");
            DontDestroyOnLoad(go);
            go.AddComponent<ReplayDatasetExporter>();
        }

        public void Awake() {
            Debug.Log("[BotDataset] Exporter loaded and waiting for a replay.");
            QuantumCallback.Subscribe<CallbackSimulateFinished>(this, OnSimulateFinished);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
        }

        public void OnApplicationQuit() {
            Close();
        }

        //---Callbacks
        private void OnGameDestroyed(CallbackGameDestroyed e) {
            Close();
        }

        private int diagCounter;

        private void OnSimulateFinished(CallbackSimulateFinished e) {
            var manager = ActiveReplayManager.Instance;
            if (diagCounter++ % 600 == 0) {
                Debug.Log("[BotDataset] DIAG tick=" + e.Frame.Number + " manager=" + (manager != null) + " isReplay=" + (manager != null && manager.IsReplay) + " rows=" + rowCount);
            }
            if (manager == null || !manager.IsReplay) {
                return;
            }

            if (manager.CurrentReplay != currentReplay) {
                Close();
                Open(manager.CurrentReplay);
            }

            Frame f = e.Frame;
            if (f.Global->GameState != GameState.Playing) {
                return;
            }

            int tick = f.Number;
            if (tick <= lastHandledTick) {
                // Rewind / duplicate tick. Ignore until we pass where we were.
                return;
            }
            lastHandledTick = tick;

            if (tick % TickStride == 0 && pendingTick == tick - 1) {
                WriteRows(f, tick, manager.ReplayStart);
            }
            if ((tick + 1) % TickStride == 0) {
                ComputePending(f, tick, manager.ReplayStart);
            }
        }

        //---File handling
        private void Open(BinaryReplayFile replay) {
            currentReplay = replay;
            pending.Clear();
            lastStars.Clear();
            names.Clear();
            pendingTick = int.MinValue;
            lastHandledTick = int.MinValue;
            rowCount = 0;

            try {
                string dir = Path.Combine(Application.persistentDataPath, "BotDataset");
                Directory.CreateDirectory(dir);
                string replayName = string.IsNullOrEmpty(replay?.FilePath) ? "replay" : Path.GetFileNameWithoutExtension(replay.FilePath);
                basePath = Path.Combine(dir, replayName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                writer = new StreamWriter(basePath + ".csv", false, new UTF8Encoding(false), 1 << 16);
                writer.WriteLine(BuildHeader());
                Debug.Log("[BotDataset] Recording started: " + basePath + ".csv");
            } catch (Exception ex) {
                Debug.LogError("[BotDataset] Could not open output file: " + ex);
                writer = null;
            }
        }

        private void Close() {
            if (writer != null) {
                try {
                    writer.Flush();
                    writer.Dispose();

                    var header = currentReplay?.Header;
                    var sbp = new StringBuilder("player,nickname,last_seen_stars,header_final_stars,header_team,is_winning_team\n");
                    foreach (var kvp in lastStars) {
                        names.TryGetValue(kvp.Key, out string nick);
                        nick = (nick ?? "").Replace(",", " ").Replace("\n", " ");
                        string finalStars = "", team = "", won = "";
                        if (header != null) {
                            foreach (var info in header.PlayerInformation) {
                                if ((int) info.PlayerRef == kvp.Key) {
                                    finalStars = info.FinalObjectiveCount.ToString();
                                    team = info.Team.ToString();
                                    won = info.Team == header.WinningTeam ? "1" : "0";
                                }
                            }
                        }
                        sbp.Append(kvp.Key).Append(',').Append(nick).Append(',').Append(kvp.Value).Append(',')
                            .Append(finalStars).Append(',').Append(team).Append(',').Append(won).Append('\n');
                    }
                    File.WriteAllText(basePath + "_players.csv", sbp.ToString());
                    if (header != null) {
                        File.WriteAllText(basePath + "_meta.txt",
                            "version=" + header.Version + "\n" +
                            "name=" + header.CustomName + "\n" +
                            "length_frames=" + header.ReplayLengthInFrames + "\n" +
                            "winning_team=" + header.WinningTeam + "\n" +
                            "rules=" + JsonConvert.SerializeObject(header.Rules, Formatting.None) + "\n");
                    }
                    Debug.Log("[BotDataset] Recording finished. " + rowCount + " rows written to " + basePath + ".csv");
                } catch (Exception ex) {
                    Debug.LogError("[BotDataset] Error while closing output file: " + ex);
                }
            }
            writer = null;
            currentReplay = null;
        }

        //---Header
        private static string BuildHeader() {
            var h = new StringBuilder();
            h.Append("tick,player,stage,stars,x,y,vx,vy,facing_right,powerup,has_reserve,on_ground,wall_left,wall_right,jump_state,");
            h.Append("crouching,groundpounding,sliding,wallsliding,in_shell,propeller_flying,knockback,star_power,damage_invincible,holding,underwater,alive_players,lives,team,stars_to_win,start_lives,teams_on,custom_powerups,team_attack,timer_minutes");
            for (int i = 1; i <= NearestStars; i++) h.Append($",star{i}_dx,star{i}_dy");
            for (int i = 1; i <= NearestPlayers; i++) h.Append($",p{i}_dx,p{i}_dy,p{i}_vx,p{i}_vy,p{i}_powerup,p{i}_stars,p{i}_same_team");
            for (int i = 1; i <= NearestEnemies; i++) h.Append($",enemy{i}_dx,enemy{i}_dy");
            for (int i = 1; i <= NearestPowerups; i++) h.Append($",item{i}_dx,item{i}_dy");
            h.Append(",tiles");
            h.Append(",up,down,left,right,jump,sprint,powerup_btn,fireball_btn,propeller_btn");
            return h.ToString();
        }

        //---Data collection
        private void ComputePending(Frame f, int tick, int replayStart) {
            pending.Clear();
            pendingTick = tick;

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            stageName = (f.Map.Scene ?? "").Replace(",", " ");
            wrapWidth = stage.IsWrappingLevel ? stage.TileDimensions.X / 2f : 0f;

            players.Clear();
            enemies.Clear();
            stars.Clear();
            powerups.Clear();

            // Players
            var marioFilter = f.Filter<Transform2D, MarioPlayer, PhysicsObject>();
            marioFilter.UseCulling = false;
            while (marioFilter.NextUnsafe(out EntityRef _, out Transform2D* t, out MarioPlayer* m, out PhysicsObject* p)) {
                if (!m->PlayerRef.IsValid || m->IsDead || m->IsRespawning) {
                    continue;
                }
                int index = (int) m->PlayerRef;
                int starCount = m->GamemodeData.StarChasers->Stars;
                players.Add(new Obj {
                    X = t->Position.X.AsFloat, Y = t->Position.Y.AsFloat,
                    VX = p->Velocity.X.AsFloat, VY = p->Velocity.Y.AsFloat,
                    Powerup = (int) m->CurrentPowerupState, Stars = starCount, Id = index, Team = m->GetTeam(f) ?? -1,
                });
                lastStars[index] = starCount;
                if (!names.ContainsKey(index)) {
                    names[index] = f.GetPlayerData(m->PlayerRef)?.PlayerNickname ?? "";
                }
            }

            // Big stars
            var starFilter = f.Filter<Transform2D, BigStar>();
            starFilter.UseCulling = false;
            while (starFilter.NextUnsafe(out EntityRef _, out Transform2D* t, out BigStar* s)) {
                stars.Add(new Obj { X = t->Position.X.AsFloat, Y = t->Position.Y.AsFloat, Id = -1 });
            }

            // Enemies
            var enemyFilter = f.Filter<Transform2D, Enemy>();
            enemyFilter.UseCulling = false;
            while (enemyFilter.NextUnsafe(out EntityRef _, out Transform2D* t, out Enemy* en)) {
                if (en->IsDead || !en->IsActive) {
                    continue;
                }
                enemies.Add(new Obj { X = t->Position.X.AsFloat, Y = t->Position.Y.AsFloat, Id = -1 });
            }

            // Powerup items lying around
            var powerupFilter = f.Filter<Transform2D, Powerup>();
            powerupFilter.UseCulling = false;
            while (powerupFilter.NextUnsafe(out EntityRef _, out Transform2D* t, out Powerup* pw)) {
                powerups.Add(new Obj { X = t->Position.X.AsFloat, Y = t->Position.Y.AsFloat, Id = -1 });
            }

            // One feature string per living player
            marioFilter = f.Filter<Transform2D, MarioPlayer, PhysicsObject>();
            marioFilter.UseCulling = false;
            while (marioFilter.NextUnsafe(out EntityRef _, out Transform2D* t, out MarioPlayer* m, out PhysicsObject* p)) {
                if (!m->PlayerRef.IsValid || m->IsDead || m->IsRespawning) {
                    continue;
                }
                pending[(int) m->PlayerRef] = BuildFeatures(f, stage, tick - replayStart, m, t, p);
            }
        }

        private string BuildFeatures(Frame f, VersusStageData stage, int relativeTick, MarioPlayer* m, Transform2D* t, PhysicsObject* p) {
            sb.Clear();
            int index = (int) m->PlayerRef;
            float x = t->Position.X.AsFloat;
            float y = t->Position.Y.AsFloat;

            sb.Append(relativeTick).Append(',').Append(index).Append(',').Append(stageName).Append(',').Append((int) m->GamemodeData.StarChasers->Stars);
            Num(x); Num(y); Num(p->Velocity.X.AsFloat); Num(p->Velocity.Y.AsFloat);
            Flag(m->FacingRight);
            sb.Append(',').Append((int) m->CurrentPowerupState);
            Flag(m->ReserveItem.Id.IsValid);
            Flag(p->IsTouchingGround);
            Flag(p->IsTouchingLeftWall);
            Flag(p->IsTouchingRightWall);
            sb.Append(',').Append((int) m->JumpState);
            Flag(m->IsCrouching);
            Flag(m->IsGroundpounding);
            Flag(m->IsSliding);
            Flag(m->IsWallsliding);
            Flag(m->IsInShell);
            Flag(m->IsPropellerFlying);
            sb.Append(',').Append((int) m->CurrentKnockback);
            Flag(m->IsStarmanInvincible);
            Flag(m->DamageInvincibilityFrames > 0);
            Flag(m->HeldEntity != default);
            Flag(p->IsUnderwater);
            sb.Append(',').Append(players.Count);
            int myTeam = m->GetTeam(f) ?? -1;
            sb.Append(',').Append((int) m->Lives).Append(',').Append(myTeam);
            sb.Append(',').Append(f.Global->Rules.StarsToWin);
            sb.Append(',').Append(f.Global->Rules.Lives);
            Flag(f.Global->Rules.TeamsEnabled);
            Flag(f.Global->Rules.CustomPowerupsEnabled);
            sb.Append(',').Append((int) f.Global->Rules.TeamAttack);
            sb.Append(',').Append(f.Global->Rules.TimerMinutes);

            AppendNearest(stars, x, y, -1, -1, NearestStars, false);
            AppendNearest(players, x, y, index, myTeam, NearestPlayers, true);
            AppendNearest(enemies, x, y, -1, -1, NearestEnemies, false);
            AppendNearest(powerups, x, y, -1, -1, NearestPowerups, false);

            // Terrain grid, top row first. 1 = solid-ish tile, 0 = empty.
            sb.Append(',');
            for (int dy = GridHalfHeight; dy >= -GridHalfHeight; dy--) {
                for (int dx = -GridHalfWidth; dx <= GridHalfWidth; dx++) {
                    // One tile = 0.5 world units.
                    FPVector2 pos = t->Position + new FPVector2(dx, dy) / 2;
                    sb.Append(TileCode(f, stage.GetTileWorld(f, pos)));
                }
            }

            return sb.ToString();
        }

        /// <summary>0 = empty, 1 = solid, 2 = slippery (ice), 3 = damaging (spikes, lava blocks...), 4 = block (brick, ? block, coin block).</summary>
        private static char TileCode(Frame f, StageTileInstance tile) {
            if (tile.Tile == default) {
                return '0';
            }
            StageTile asset = f.FindAsset(tile.Tile);
            if (asset == null) {
                return '0';
            }
            if (asset is DamagingTile) {
                return '3';
            }
            if (asset is BreakableBrickTile) {
                return '4';
            }
            if (!tile.HasWorldPolygons(asset)) {
                return '0';
            }
            return asset.IsSlipperyGround ? '2' : '1';
        }

        private float Wrap(float dx) {
            if (wrapWidth > 0f) {
                dx -= wrapWidth * Mathf.Round(dx / wrapWidth);
            }
            return dx;
        }

        private void AppendNearest(List<Obj> list, float x, float y, int selfId, int selfTeam, int count, bool extras) {
            int n = Math.Min(list.Count, used.Length);
            for (int k = 0; k < count; k++) {
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < n; i++) {
                    if (used[i]) {
                        continue;
                    }
                    Obj o = list[i];
                    if (selfId >= 0 && o.Id == selfId) {
                        continue;
                    }
                    float dx = Wrap(o.X - x), dy = o.Y - y;
                    float d = dx * dx + dy * dy;
                    if (d < bestDist) {
                        bestDist = d;
                        best = i;
                    }
                }

                if (best < 0) {
                    sb.Append(',').Append(Missing).Append(',').Append(Missing);
                    if (extras) {
                        sb.Append(",0,0,0,0,0");
                    }
                } else {
                    used[best] = true;
                    Obj o = list[best];
                    Num(Wrap(o.X - x)); Num(o.Y - y);
                    if (extras) {
                        Num(o.VX); Num(o.VY);
                        sb.Append(',').Append(o.Powerup).Append(',').Append(o.Stars);
                        sb.Append(selfTeam >= 0 && o.Team == selfTeam ? ",1" : ",0");
                    }
                }
            }
            Array.Clear(used, 0, n);
        }

        private void WriteRows(Frame f, int tick, int replayStart) {
            if (writer == null) {
                return;
            }

            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;
            while (marioFilter.NextUnsafe(out EntityRef _, out MarioPlayer* m)) {
                if (!m->PlayerRef.IsValid) {
                    continue;
                }
                if (!pending.TryGetValue((int) m->PlayerRef, out string features)) {
                    continue;
                }
                global::Quantum.Input* input = f.GetPlayerInput(m->PlayerRef);
                if (input == null) {
                    continue;
                }

                sb.Clear();
                sb.Append(features);
                Flag(input->Up.IsDown);
                Flag(input->Down.IsDown);
                Flag(input->Left.IsDown);
                Flag(input->Right.IsDown);
                Flag(input->Jump.IsDown);
                Flag(input->Sprint.IsDown);
                Flag(input->PowerupAction.IsDown);
                Flag(input->FireballPowerupAction.IsDown);
                Flag(input->PropellerPowerupAction.IsDown);
                writer.WriteLine(sb.ToString());
                rowCount++;
            }
        }

        //---Helpers
        private void Num(float value) {
            sb.Append(',').Append(value.ToString("F2", CultureInfo.InvariantCulture));
        }

        private void Flag(bool value) {
            sb.Append(value ? ",1" : ",0");
        }
    }
}

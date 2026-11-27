using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P2Trine.Ai;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Trine;

// 我方：P2 收尾異三角的獨立場景（Owner 11-25「拆出來」）。正式戰接在 Forsaken 採塔之後，
// 這裡開場直接讀三角（Owner 11-26 拔掉前面的光之審判），時刻與版面見 UmadP2Trine。
public sealed class UmadP2TrineScenario : IScenario
{
    public string Name => "Trine";
    public IPhase Phase => UmadZone.P2;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP2TrineAi()];

    // 三角本體是 EObj（0x1EBFB2 朝東／0x1EBFB3 朝西），依實戰錄製逐波出現：每波現身後 1.3 秒落地（EObjAnim 0010/0020，
    // 10.7 秒後爆）、2.85 秒定住（0040/0080），爆炸前 0.1 秒收尾（0004/0008），爆後 1.25 秒消失。地上不另畫範圍。
    private static readonly (uint State, uint Bitmask) EObjAnimLand = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimSettle = (0x0040, 0x0080);
    private static readonly (uint State, uint Bitmask) EObjAnimBlast = (0x0004, 0x0008);
    private const float LandDelay = 1.3f;
    private const float SettleDelay = 2.85f;
    private const float BlastBeatLead = 0.1f;
    private const float DespawnDelay = 1.25f;

    private UmadP2TrineState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? kefka;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP2TrineState(world.Rng);
        DiagnosticLog.Info($"Trine layout {state.Trine} wings={(state.WingsRight ? "R" : "L")}");
        if (selectedAi is { } idx && idx >= 0 && idx < AiStrats.Count)
            ((IScenarioAi<UmadP2TrineState>)AiStrats[idx]).Run(state, world);
        Run_Kefka();
    }

    private void Run_Kefka()
    {
        var faceNorth = new Vector3(0f, 0f, -10f);
        kefka = world.SpawnEnemy(new EnemySpawnConfig(BNpcBaseId: BNpcBaseId.GodKefka, NameId: BNpcNameId.Kefka, Level: 100, Targetable: true, EnemyList: EnemyListMode.Always, Visibility: SpawnVisibility.Visible, LayoutId: BNpcLayoutId.GodKefka, Placement: new Placement(Vector3.Zero, 0f)));
        world.Events.Add(UmadP2Trine.TrineCastAt, () =>
        {
            kefka?.Face(faceNorth);
            kefka?.Cast(ActionId.TrineCast, animationLock: 1.1f);
        });
        world.Events.Add(UmadP2Trine.WingsCastAt, () =>
        {
            kefka?.Face(faceNorth);
            kefka?.Cast(state.WingsRight ? UmadActions.WingsOfDestructionRight : UmadActions.WingsOfDestructionLeft);
        });

        for (var wave = 0; wave < UmadP2Trine.WaveAt.Length; wave++)
        {
            var vertices = state.Trine.WaveVertices(wave);
            var blastAt = UmadP2Trine.WaveAt[wave];
            var triangles = new List<SimEventObject?>();
            // 同一波可能分兩個時刻現身（第三波的中心三角較晚，見 UmadP2Trine.CenterAppearAt）。
            foreach (var group in state.Trine.WaveCells(wave).GroupBy(c => c.AppearAt))
            {
                var appearAt = group.Key;
                var cells = group.ToList();
                var spawned = new List<SimEventObject?>();
                world.Events.Add(appearAt, () =>
                {
                    foreach (var (centroid, pointsEast, layoutId, _) in cells)
                        spawned.Add(world.SpawnEventObject(new EventObjectSpawnConfig
                        {
                            EObjId = pointsEast ? EObjId.TrineEast : EObjId.TrineWest,
                            LayoutId = layoutId,
                            EventId = EObjId.PropEventId,
                            UseSyntheticEntityId = true, // PlayBeat 只對 synthetic id 送
                            TargetableStatus = 5,
                            TimelineState = 2,
                            Placement = new Placement(new Vector3(centroid.X, 0f, centroid.Y), 0f),
                        }));
                    triangles.AddRange(spawned);
                });
                world.Events.Add(appearAt + LandDelay, () => spawned.ForEach(t => t?.PlayBeat(EObjAnimLand.State, EObjAnimLand.Bitmask)));
                world.Events.Add(appearAt + SettleDelay, () => spawned.ForEach(t => t?.PlayBeat(EObjAnimSettle.State, EObjAnimSettle.Bitmask)));
            }
            world.Events.Add(blastAt - BlastBeatLead, () => triangles.ForEach(t => t?.PlayBeat(EObjAnimBlast.State, EObjAnimBlast.Bitmask)));
            world.Events.Add(blastAt, () =>
            {
                foreach (var v in vertices)
                    kefka?.CastFromHelper(UmadActions.Trine, at: new Placement(new Vector3(v.X, 0f, v.Y), 0f));
            });
            world.Events.Add(blastAt + DespawnDelay, () =>
            {
                triangles.ForEach(t => t?.Despawn());
                triangles.Clear();
            });
        }

        // 死刑打離 Kefka 最近與最遠的人（BossMod P2WingsOfDestructionBuster）。
        world.Events.Add(UmadP2Trine.BusterCastAt, () => kefka?.Cast(ActionId.WingsOfDestructionBusterCast, animationLock: 1.1f));
        world.Events.Add(UmadP2Trine.BusterHitAt, () =>
        {
            if (kefka == null) return;
            var closest = party.Find.Closest(kefka.Position);
            var farthest = party.Find.Farest(kefka.Position);
            if (closest != null) kefka.CastFromHelper(UmadActions.WingsOfDestructionBuster, closest);
            if (farthest != null && !ReferenceEquals(farthest, closest))
                kefka.CastFromHelper(UmadActions.WingsOfDestructionBuster, farthest);
        });
        // 這段的仇恨坦同 Forsaken 普攻（OT）。
        world.Events.Add(UmadP2Trine.UltimateEmbraceAt, () => kefka?.Cast(UmadActions.UltimateEmbrace, party.Get(PartyRole.OffTank)));
    }
}

using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.P2Trine.UmadP2Trine;

namespace AnoMech.Scenarios.Umad.P2Trine.Ai;

// 我方：異三角的隊友走位（打法 Owner D2273 a 定案）。
// 依兩場國際服通關 log 的實際走位（FFLogs 3KCNG78qzPVTWmhA#8、MJFfvcmVxTwb49ph#7）：
// 全隊先到場中 → 毀滅之翼讀條時閃到另一側 → 回場中等第一波 → 第一波爆完全隊走進第一波的一格、
// 遠坦（MT）進另一格第一波並站到外緣、近坦（OT）貼在 Kefka 旁的安全點吃最近死刑 → 死刑後 MT 回 OT 身邊分攤 Ultimate Embrace。
// 站位怎麼挑見 UmadP2Trine.PlanPositions。
public sealed class UmadP2TrineAi : IScenarioAi<UmadP2TrineState>
{
    // 毀滅之翼的安全側離中心線多遠（矩形從 Kefka 中心往一側展開）。
    private const float WingsDodge = 4f;

    public string Name => "First-wave cell (NA logs)";

    public void Run(UmadP2TrineState state, SimWorld world)
    {
        var ai = new AiManager(world, upstreamArrival: true);
        var plan = state.Trine.PlanPositions();
        var safeSide = state.WingsRight ? -WingsDodge : WingsDodge;

        ai.Move(0.5f, () => AiMove.All(Vector2.Zero), arrivalTime: TrineCastAt + 2f);
        ai.Move(WingsCastAt + 0.3f, () => AiMove.All(new Vector2(safeSide, 0f)), arrivalTime: WingsResolveAt - 1f);
        ai.Move(WingsResolveAt + 0.2f, () => AiMove.All(Vector2.Zero), arrivalTime: WingsResolveAt + 2.5f);
        ai.Move(WaveAt[0] + 0.15f, () => Spots(plan.Party, plan.FarTank, plan.CloseTank), arrivalTime: WaveAt[2] - 1f);
        ai.Move(BusterHitAt + 0.2f, () => Spots(null, plan.CloseTank, null), arrivalTime: UltimateEmbraceAt + 3f);
    }

    // 座位順序同 PartyRole：MT、OT、兩補、四 DPS；null＝那一組不動。
    private static IAiMove Spots(Vector2? party, Vector2? mainTank, Vector2? offTank)
    {
        var coords = new Vector2?[8];
        for (var i = 0; i < coords.Length; i++) coords[i] = party;
        coords[(int)PartyRole.MainTank] = mainTank;
        coords[(int)PartyRole.OffTank] = offTank;
        return AiMove.Create(coords).NaturalOrder();
    }
}

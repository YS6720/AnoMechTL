using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.P2Trine.UmadP2Trine;

namespace AnoMech.Scenarios.Umad.P2Trine.Ai;

// 我方：異三角的隊友走位（Owner 11-27 D2686 a）。
// 全隊預設在王的南側等 → 毀滅之翼讀條時閃到另一側（仍在南側）→ 回南側等第一波 → 第一波爆完：
// 遠坦（MT）走到從 A 逆時針第一個安全格那個方向的場邊、近坦（OT）在同方向靠王的安全點吃最近死刑、人群進南半場的安全格
// → 死刑後 MT 回 OT 身邊分攤 Ultimate Embrace。格怎麼挑見 UmadP2Trine.PlanPositions。
public sealed class UmadP2TrineAi : IScenarioAi<UmadP2TrineState>
{
    // 毀滅之翼的安全側離中心線多遠（矩形從 Kefka 中心往一側展開）。
    private const float WingsDodge = 4f;
    // 等待點在王南側多遠：離第一波最近的頂點（南格的 (0,10)）仍有 7y，大於 r6。
    private const float WaitSouth = 3f;

    public string Name => "Tanks first from A, party south";

    public void Run(UmadP2TrineState state, SimWorld world)
    {
        var ai = new AiManager(world, upstreamArrival: true);
        var plan = state.Trine.PlanPositions();
        var wait = new Vector2(0f, WaitSouth);
        var safeSide = state.WingsRight ? -WingsDodge : WingsDodge;

        ai.Move(0.5f, () => AiMove.All(wait), arrivalTime: TrineCastAt + 2f);
        ai.Move(WingsCastAt + 0.3f, () => AiMove.All(new Vector2(safeSide, WaitSouth)), arrivalTime: WingsResolveAt - 1f);
        ai.Move(WingsResolveAt + 0.2f, () => AiMove.All(wait), arrivalTime: WingsResolveAt + 2.5f);
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

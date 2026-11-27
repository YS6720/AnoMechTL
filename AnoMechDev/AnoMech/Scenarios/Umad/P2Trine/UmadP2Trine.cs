using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AnoMech.Scenarios.Umad.P2Trine;

// 我方：P2 收尾「異三角」（Trine＋Wings of Destruction＋Ultimate Embrace），Owner 11-25 要求補上、同日要求拆成獨立場景。
// 純邏輯（版面、時刻、站位規劃），Tests 直接連結；施放在 UmadP2TrineScenario，隊友移動在 Ai/UmadP2TrineAi。
//
// 來源：FFLogs 國際服通關 log（3KCNG78qzPVTWmhA#8、MJFfvcmVxTwb49ph#7 等 15 場，2026-11-25 取）
// ＋ BossMod UMAD/P2Trines.cs（邊長 10 正三角、每頂點 r6 圓、落地後 10.7 秒爆）。
// - 七個三角固定在同一組格位：中心一個＋外圈六個（以中心為軸每 60° 一個的風車排列），外圈各自的尖端朝向固定；
//   15 場全部是「先爆 3 個外圈 → 2 秒後 1 個外圈 → 再 2 秒後 2 個外圈＋中心」。
// - 中心永遠最後一波；外圈哪幾個進哪一波、中心尖端朝東或朝西，各場不同。
//   UNVERIFIED：外圈分波是否有限制。15 場看不出規律，這裡當成均勻隨機。
// - 座標是場景座標（中心 0,0；北＝-Z），FFLogs 的 (x, y) 減 100 即得。
public sealed record UmadP2Trine(int[] FirstWave, int SecondWave, int[] LastWave, bool CenterPointsEast)
{
    public const float EdgeLength = 10f;
    public const float BlastRadius = 6f;
    public static readonly float Circumradius = EdgeLength * MathF.Sqrt(3f) / 3f;

    // 外圈六格，依角度順時針（從西北起）；PointsEast＝單一尖端在東側。
    public static readonly IReadOnlyList<(Vector2 Centroid, bool PointsEast)> Ring =
    [
        (new(-2f * Circumradius, -10f), false),  // 西北
        (new(0.5f * Circumradius, -15f), true),  // 北
        (new(2.5f * Circumradius, -5f), false),  // 東北
        (new(2f * Circumradius, 10f), true),     // 東南
        (new(-0.5f * Circumradius, 15f), false), // 南
        (new(-2.5f * Circumradius, 5f), true),   // 西南
    ];

    // 時刻（場景秒）：獨立場景開場 2 秒直接讀三角（Owner 11-26 要求拔掉前面的光之審判與等待），
    // 其餘偏移取兩場 log 相對三角讀條的實測（兩場差 ≤0.06 秒）。
    public const float TrineCastAt = 2f;
    public const float WingsCastAt = TrineCastAt + 6.15f;
    public const float WingsResolveAt = WingsCastAt + 4.0f;
    public static readonly float[] WaveAt = [TrineCastAt + 15.86f, TrineCastAt + 17.87f, TrineCastAt + 19.87f];
    // 實戰錄製：外圈三角依爆炸順序逐波現身，第一波在三角讀條開始後約 4.0 秒，之後每波晚 2 秒；現身 1.3 秒後落地（EObjAnim 00100020），
    // BossMod：落地後 10.7 秒爆。
    public const float FallLead = 10.7f;
    public static float AppearAt(int wave) => TrineCastAt + 4.0f + 2f * wave;
    // 中心三角實戰跟第三波一起在毀滅之翼讀條中現身；Owner 11-27（D2696 b）要求讀條期間王腳下不顯示三角，
    // 改在毀滅之翼結算時才出現（比實戰晚約 2.15 秒，爆炸時刻不變）。
    public const float CenterAppearAt = WingsResolveAt;
    public const float BusterCastAt = TrineCastAt + 16.31f;
    public const float BusterHitAt = TrineCastAt + 20.36f;
    public const float BusterRadius = 7f;
    public const float UltimateEmbraceAt = TrineCastAt + 22.46f;
    public const float UltimateEmbraceRadius = 5f;

    public static UmadP2Trine Roll(Rng rng)
    {
        var ring = rng.Shuffle(Enumerable.Range(0, Ring.Count)).ToArray();
        return new UmadP2Trine(ring[..3], ring[3], ring[4..], rng.NextBool());
    }

    // wave 0–2 爆炸的所有頂點。
    public IReadOnlyList<Vector2> WaveVertices(int wave) => wave switch
    {
        0 => FirstWave.SelectMany(RingVertices).ToList(),
        1 => RingVertices(SecondWave).ToList(),
        _ => LastWave.SelectMany(RingVertices).Concat(Vertices(Vector2.Zero, CenterPointsEast)).ToList(),
    };

    // wave 0–2 現身的三角：(中心點, 尖端朝東, LayoutId, 現身時刻)；中心永遠在最後一波，現身時刻見 CenterAppearAt。
    public IReadOnlyList<(Vector2 Centroid, bool PointsEast, uint LayoutId, float AppearAt)> WaveCells(int wave)
    {
        var ring = wave switch { 0 => FirstWave, 1 => new[] { SecondWave }, _ => LastWave };
        var cells = ring.Select(i => (Ring[i].Centroid, Ring[i].PointsEast, UmadConstants.EObjId.TrineRingLayoutIds[i], AppearAt(wave))).ToList();
        if (wave >= 2) cells.Add((Vector2.Zero, CenterPointsEast, UmadConstants.EObjId.TrineCenterLayoutId, CenterAppearAt));
        return cells;
    }

    public static IEnumerable<Vector2> RingVertices(int index) => Vertices(Ring[index].Centroid, Ring[index].PointsEast);

    private static IEnumerable<Vector2> Vertices(Vector2 centroid, bool pointsEast)
    {
        var tip = pointsEast ? 1f : -1f;
        yield return centroid + new Vector2(tip * Circumradius, 0f);
        yield return centroid + new Vector2(-tip * Circumradius / 2f, -EdgeLength / 2f);
        yield return centroid + new Vector2(-tip * Circumradius / 2f, EdgeLength / 2f);
    }

    // ── 站位規劃（打法：Owner D2686 a，見 Ai/UmadP2TrineAi） ──

    // 遠坦站在自己那格中心再往外，確保比站在格中心的全隊遠（死刑打最近與最遠）。
    public const float FarTankRadius = 18.5f;
    // 近坦點與其他人保持的距離：死刑 r7（也涵蓋 Ultimate Embrace r5）＋ 1.5 餘裕。
    private const float CloseTankClearance = BusterRadius + 1.5f;
    // 近坦點與之後才爆的頂點保持的距離：r6 ＋ 1 餘裕。
    private const float VertexClearance = BlastRadius + 1f;

    public sealed record Plan(Vector2 Party, Vector2 FarTank, Vector2 CloseTank);

    // 第一波爆完後的三個安全格，從 A（北）起逆時針（北→西→南→東）排：坦克先拿第一格，遠坦走到該格外緣、近坦靠王；
    // 人群拿剩下格中在南半場、順序最前的一格，南半場沒有就拿剩下的第一格（Owner 11-27）。
    public Plan PlanPositions()
    {
        var scan = FirstWave.OrderBy(i => CounterClockwiseFromA(Ring[i].Centroid)).ToList();
        var tankIndex = scan[0];
        var partyIndex = scan.Skip(1).Where(i => Ring[i].Centroid.Y > 0f).DefaultIfEmpty(scan[1]).First();
        var party = Ring[partyIndex].Centroid;
        var farTank = Vector2.Normalize(Ring[tankIndex].Centroid) * FarTankRadius;
        return new Plan(party, farTank, CloseTankSpot(party, farTank));
    }

    // 由正北往西量的角度（北＝-Y）。外圈每格各占 60° 扇區，北格中心在北偏東約 11°，
    // 起點往東退 30°，讓 A 所在的北格排第一。
    private static float CounterClockwiseFromA(Vector2 centroid)
    {
        var angle = MathF.Atan2(-centroid.X, -centroid.Y) + MathF.PI / 6f;
        return angle < 0f ? angle + 2f * MathF.PI : angle;
    }

    // 從遠坦方向起左右各 5° 一步（到 ±60°），每個方向由內往外 0.25y 一步（6–11y），取「半徑＋偏角」分數最低的安全點。
    private Vector2 CloseTankSpot(Vector2 party, Vector2 farTank)
    {
        var later = WaveVertices(1).Concat(WaveVertices(2)).ToList();
        var baseAngle = MathF.Atan2(farTank.Y, farTank.X);
        Vector2? best = null;
        var bestScore = float.MaxValue;
        for (var step = 0; step <= 12; step++)
            foreach (var sign in step == 0 ? new[] { 0 } : new[] { 1, -1 })
            {
                var angle = baseAngle + sign * step * MathF.PI / 36f;
                for (var radius = 6f; radius <= 11f; radius += 0.25f)
                {
                    var spot = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (later.Any(v => Vector2.Distance(v, spot) < VertexClearance)) continue;
                    if (Vector2.Distance(spot, party) < CloseTankClearance || Vector2.Distance(spot, farTank) < CloseTankClearance)
                        continue;
                    var score = radius + step / 6f;
                    if (score < bestScore) (best, bestScore) = (spot, score);
                    break;
                }
            }
        // 60 種分波 × 中心朝向全部有解（Tests 逐一驗）；找不到代表版面資料被改壞。
        return best ?? throw new InvalidOperationException($"異三角找不到近坦安全點：{this}");
    }

    public override string ToString() =>
        $"wave1[{string.Join(",", FirstWave)}] wave2[{SecondWave}] wave3[{string.Join(",", LastWave)}]+centre({(CenterPointsEast ? "E" : "W")})";
}

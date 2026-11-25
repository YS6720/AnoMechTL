namespace AnoMech.Scenarios.Umad.P2Trine;

// 每輪抽一次的異三角版面與毀滅之翼左右；Run 期間不變，場景與隊友 AI 共用。
public sealed class UmadP2TrineState(Rng rng)
{
    public UmadP2Trine Trine { get; } = UmadP2Trine.Roll(rng);

    // WingsRight＝毀滅之翼打 Kefka 右手邊（面北時的東半場）。
    public bool WingsRight { get; } = rng.NextBool();
}

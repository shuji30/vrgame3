namespace VRGame3.Sports.TableTennis
{
    /// <summary>卓球の台のどちら側か。プレイヤーは -Z 側、CPU は +Z 側。</summary>
    public enum TTSide
    {
        Player,
        Cpu,
    }

    /// <summary>TTSide の小さな計算。</summary>
    public static class TTSideExtensions
    {
        public static TTSide Other(this TTSide side) => side == TTSide.Player ? TTSide.Cpu : TTSide.Player;
    }
}

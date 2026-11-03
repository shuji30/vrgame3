namespace VRGame3.Sports.TableTennis
{
    /// <summary>TTSide の小さな計算。</summary>
    public static class TTSideExtensions
    {
        public static TTSide Other(this TTSide side) => side == TTSide.Player ? TTSide.Cpu : TTSide.Player;
    }
}

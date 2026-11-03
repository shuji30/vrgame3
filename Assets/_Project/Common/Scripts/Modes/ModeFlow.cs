using System;
using System.Collections.Generic;

namespace VRGame3.Common
{
    /// <summary>
    /// 交代プレイとチャレンジの画面の流れ(3種目で共通)。ChoicePanel に出す中身とボタンをまとめる。
    /// docs/plan.md「交代プレイ」「チャレンジ」、見た目は docs/art-direction.md 6.11・6.12。
    /// </summary>
    public static class ModeFlow
    {
        /// <summary>
        /// 交代プレイで1人のゲームが終わったとき。スコアを記録し、全員終わっていれば順位表、まだなら交代の案内を出す。
        /// startNext は次の人の「はじめに」パネルを出す処理、retry は同じ人数でもう一度始める処理。
        /// </summary>
        public static void FinishTurn(ChoicePanel panel, int score, string unit, Action startNext, Action retry)
        {
            int player = TurnSession.Current;
            TurnSession.RecordScore(score);
            if (TurnSession.AllDone)
            {
                ShowRanking(panel, unit, retry);
                return;
            }

            int next = TurnSession.Current;
            panel.Show($"{TurnSession.Label(player)}  {score}{unit}",
                $"つぎは {TurnSession.Label(next)} の番です。\nヘッドセットとコントローラーを渡してください。",
                new ChoicePanel.Option("はじめる", startNext, true));
            panel.PlaySound(panel.TurnChangeClip);
        }

        private static void ShowRanking(ChoicePanel panel, string unit, Action retry)
        {
            IReadOnlyList<int> scores = TurnSession.PlayerScores;
            var order = new List<int>();
            for (int i = 0; i < scores.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) => scores[b] != scores[a] ? scores[b].CompareTo(scores[a]) : a.CompareTo(b));
            string body = string.Empty;
            foreach (int i in order)
            {
                body += $"{TurnSession.RankOf(i)}位  {TurnSession.Label(i)}  {scores[i]}{unit}\n";
            }

            int count = TurnSession.PlayerCount;
            // 全員が終わったので、保存した身長・利き手に戻す
            TurnSession.End();
            panel.Show("じゅんい", body.TrimEnd(),
                new ChoicePanel.Option("もういちど", () =>
                {
                    TurnSession.Begin(count);
                    retry();
                }, true),
                new ChoicePanel.Option("ハブへ", () => SceneTransition.Load(SceneTransition.HubScene)));
            panel.PlaySound(panel.ClearClip);
            panel.Celebrate();
        }

        /// <summary>チャレンジの一覧を出す。選んだら onSelect(ID)、「もどる」で onBack。</summary>
        public static void ShowChallengeList(ChoicePanel panel, string sportId, Action<string> onSelect, Action onBack)
        {
            List<ChallengeCatalog.Entry> entries = ChallengeCatalog.ForSport(sportId);
            var options = new List<ChoicePanel.Option>();
            foreach (ChallengeCatalog.Entry e in entries)
            {
                string id = e.Id;
                string label = SaveStore.IsChallengeCleared(id) ? $"◎ {e.Name}" : e.Name;
                options.Add(new ChoicePanel.Option(label, () => onSelect(id)));
            }

            options.Add(new ChoicePanel.Option("もどる", onBack));
            panel.Show("チャレンジ", ChallengeCatalog.ListText(sportId), options.ToArray());
        }

        /// <summary>チャレンジの結果。クリアなら記録する。</summary>
        public static void ShowChallengeResult(ChoicePanel panel, string id, bool cleared, string detail, Action retry,
            Action other)
        {
            ChallengeCatalog.Entry entry = ChallengeCatalog.Find(id);
            if (cleared && SaveStore.MarkChallengeCleared(id))
            {
                // クリアの数が 3・6・9 になったら、新しいリストバンドが手に入る
                int count = SaveStore.Data.clearedChallenges.Count;
                if (count % 3 == 0 && count <= 9)
                {
                    detail += $"\nあたらしい リストバンド {RewardCatalog.Name(RewardCatalog.Item.Wristband, count / 3)} が もらえた!";
                }
            }

            panel.Show(cleared ? "クリア!" : "ざんねん", $"{entry?.Name}\n{detail}",
                new ChoicePanel.Option("もういちど", retry, true),
                new ChoicePanel.Option("ほかのチャレンジ", other),
                new ChoicePanel.Option("ハブへ", () => SceneTransition.Load(SceneTransition.HubScene)));
            panel.PlaySound(cleared ? panel.ClearClip : panel.FailClip);
            if (cleared)
            {
                panel.Celebrate();
            }

            RestReminder.AtBreak();
        }
    }
}

using System;
using System.Collections.Generic;

namespace Peephole
{
    /// <summary>
    /// 唯一的真相。隐藏变量、门的状态、事件记录都在这里，只由代码（遥测、裁决器）修改。
    /// 模型永远只能"读"它的文字摘要，不能直接写它。
    /// </summary>
    public class GameState
    {
        public readonly int seed;
        public readonly Temperament temperament;
        public readonly System.Random rng;

        // 隐藏变量（0~100）
        public float suspicion; // 戒心
        public float patience;  // 耐心
        public float fear;      // 恐惧
        public float trust;     // 信任

        public DoorState door = DoorState.Closed;
        public int turn;
        public int stepsBack;
        public int knocks;          // 自上次对话以来敲门次数
        public int slams;
        public int consecutiveSilent;
        public bool hasLastIntent;
        public Intent lastIntent;
        public Threat lastThreat;

        public readonly List<string> claims = new List<string>();      // 玩家声称过的事实
        public readonly List<string> events = new List<string>();      // 待告诉模型的"刚才发生的事"
        public readonly List<string> recentLines = new List<string>(); // 最近说过的台词，用来避免重复

        public GameState(int seed, Temperament temperament)
        {
            this.seed = seed;
            this.temperament = temperament;
            rng = new System.Random(seed);
            switch (temperament)
            {
                case Temperament.Lonely:
                    suspicion = 20f; patience = 90f; fear = 10f; trust = 25f; break;
                case Temperament.Paranoid:
                    suspicion = 45f; patience = 70f; fear = 25f; trust = 5f; break;
                default:
                    suspicion = 15f; patience = 100f; fear = 5f; trust = 25f; break;
            }
        }

        static float C(float v)
        {
            if (v < 0f) return 0f;
            if (v > 100f) return 100f;
            return v;
        }

        public void Clamp()
        {
            suspicion = C(suspicion);
            patience = C(patience);
            fear = C(fear);
            trust = C(trust);
        }

        // ---- 把数值翻译成小模型更容易读懂的词 ----
        public static string Level(float v)
        {
            if (v < 20f) return "极低";
            if (v < 40f) return "低";
            if (v < 60f) return "中";
            if (v < 80f) return "高";
            return "极高";
        }

        public static string PatienceWord(float v)
        {
            if (v > 70f) return "充足";
            if (v > 40f) return "一般";
            if (v > 15f) return "不多";
            return "快耗尽";
        }

        public string DoorWord()
        {
            switch (door)
            {
                case DoorState.Chain: return "门挂着链子，开着一条缝";
                case DoorState.Open: return "门开着";
                default: return "门关着";
            }
        }

        public string DistanceWord()
        {
            if (stepsBack <= 0) return "站得很近";
            if (stepsBack == 1) return "退开了一步";
            return "站得很远";
        }

        public void AddClaim(string claim)
        {
            if (string.IsNullOrEmpty(claim)) return;
            if (claims.Contains(claim)) return;
            claims.Add(claim);
            if (claims.Count > 4) claims.RemoveAt(0);
        }

        public void RememberLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            recentLines.Add(line);
            if (recentLines.Count > 4) recentLines.RemoveAt(0);
        }

        public string Summary()
        {
            return string.Format("戒心{0:0} 耐心{1:0} 恐惧{2:0} 信任{3:0} | 门:{4} | 回合{5}",
                suspicion, patience, fear, trust, door, turn);
        }
    }
}

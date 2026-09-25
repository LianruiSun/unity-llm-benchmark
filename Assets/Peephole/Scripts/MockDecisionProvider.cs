using System.Threading;
using System.Threading.Tasks;

namespace Peephole
{
    /// <summary>
    /// 不需要模型的规则版"门后的人"。用途：没有模型时游戏也能玩；
    /// 开发表演层时不用等推理；LLM 超时或输出非法时作为兜底。
    /// </summary>
    public class MockDecisionProvider : IDecisionProvider
    {
        readonly System.Random r;

        public MockDecisionProvider(int seed)
        {
            r = new System.Random(seed);
        }

        public string Name { get { return "Mock"; } }

        public Task<Decision> DecideAsync(Situation s, CancellationToken ct)
        {
            return Task.FromResult(Decide(s));
        }

        static readonly string[] Friendly = { "你好", "您好", "谢谢", "请", "打扰", "不好意思", "对不起", "麻烦" };
        static readonly string[] Angry = { "开门", "快开", "滚", "混蛋", "妈的", "去死", "！", "!", "再不" };
        static readonly string[] Pleading = { "求", "拜托", "帮帮", "救", "可怜", "冷", "迷路", "没电" };
        static readonly string[] Deceptive = { "物业", "查水表", "煤气", "快递", "警察", "检查", "保险", "抄表", "居委会" };
        static readonly string[] ThreatHigh = { "杀", "砸开", "撞开", "踹", "烧", "弄死", "砍", "炸" };
        static readonly string[] ThreatLow = { "后悔", "等着", "走着瞧", "小心", "砸门", "不然" };

        static bool Has(string text, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
                if (text.Contains(words[i])) return true;
            return false;
        }

        Decision Decide(Situation s)
        {
            string text = s.playerText ?? "";
            GameState st = s.state;

            Threat threat = Threat.None;
            if (Has(text, ThreatHigh)) threat = Threat.High;
            else if (Has(text, ThreatLow)) threat = Threat.Low;

            Tone tone = Tone.Calm;
            if (threat != Threat.None) tone = Tone.Angry;
            else if (Has(text, Deceptive)) tone = Tone.Deceptive;
            else if (Has(text, Angry)) tone = Tone.Angry;
            else if (Has(text, Pleading)) tone = Tone.Pleading;
            else if (Has(text, Friendly)) tone = Tone.Friendly;
            else if (LooksLikeNoise(text)) tone = Tone.Nonsense;

            string claim = "";
            string[] markers = { "我是", "我叫", "我来", "我在" };
            for (int i = 0; i < markers.Length; i++)
            {
                int idx = text.IndexOf(markers[i]);
                if (idx >= 0)
                {
                    claim = PromptBuilder.Sanitize(text.Substring(idx), 16);
                    break;
                }
            }

            double x = r.NextDouble();
            Intent it;
            if (st.suspicion >= 70f || threat == Threat.High)
                it = x < 0.4 ? Intent.Refuse : x < 0.7 ? Intent.Warn : x < 0.85 ? Intent.Slam : Intent.Police;
            else if (st.trust >= 55f && st.suspicion <= 45f)
                it = x < 0.35 ? Intent.Open : x < 0.75 ? Intent.Crack : Intent.Ask;
            else if (st.trust >= 30f && st.suspicion <= 60f)
                it = x < 0.25 ? Intent.Crack : x < 0.65 ? Intent.Ask : x < 0.85 ? Intent.Silent : Intent.Refuse;
            else
                it = x < 0.3 ? Intent.Ask : x < 0.6 ? Intent.Refuse : x < 0.8 ? Intent.Silent : x < 0.9 ? Intent.Warn : Intent.Withdraw;

            string line = Arbiter.FallbackLine(st, it);
            return Decision.Make(tone, threat, claim, it, line, "mock");
        }

        static bool LooksLikeNoise(string t)
        {
            if (t.Length < 5) return false;
            int cjk = 0;
            for (int i = 0; i < t.Length; i++) if (t[i] >= 0x4E00 && t[i] <= 0x9FFF) cjk++;
            return cjk == 0 && t.IndexOf(' ') < 0;
        }
    }
}

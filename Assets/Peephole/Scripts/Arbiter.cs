using System;
using System.Collections.Generic;

namespace Peephole
{
    /// <summary>裁决之后真正发生的事。表演层只认这个，不认模型的原始输出。</summary>
    public class Outcome
    {
        public Intent intent;
        public Intent requested;
        public string line = "";
        public string stage = "";
        public EndingId ending = EndingId.None;
        public bool forced;
        public Decision decision;
        public readonly List<string> notes = new List<string>();
    }

    /// <summary>
    /// 裁决器（纯代码）：
    /// 1. 状态变化只查表，不取模型给的数字；
    /// 2. 模型的意图必须满足当前状态才会执行，否则降级；
    /// 3. 节奏保护与强制结局由代码决定；
    /// 4. 台词只做展示，会被过滤、限长、去重。
    /// </summary>
    public static class Arbiter
    {
        static readonly string[] BadWords = { "AI", "模型", "规则", "JSON", "json", "语言模型", "助手", "作为一个" };

        // ---------------------------------------------------------------
        // 强制结局：与模型无关
        // ---------------------------------------------------------------
        public static Outcome CheckForced(GameState s)
        {
            if (s.patience <= 0f) return MakeForced(s, Intent.Slam, EndingId.DrivenAway, "耐心耗尽");
            if (s.suspicion >= 92f) return MakeForced(s, Intent.Police, EndingId.Police, "戒心过高");
            return null;
        }

        static Outcome MakeForced(GameState s, Intent intent, EndingId ending, string reason)
        {
            Outcome o = new Outcome();
            o.intent = intent;
            o.requested = intent;
            o.ending = ending;
            o.forced = true;
            o.notes.Add("强制：" + reason);
            o.line = FallbackLine(s, intent);
            o.stage = StageFor(s, intent);
            if (intent == Intent.Slam) s.door = DoorState.Closed;
            return o;
        }

        // ---------------------------------------------------------------
        // 一回合的裁决
        // ---------------------------------------------------------------
        public static Outcome Resolve(GameState s, Decision d, string playerText)
        {
            Outcome o = new Outcome();
            o.decision = d;
            o.requested = d.intentE;

            ApplyRead(s, d, o.notes);
            if (!string.IsNullOrEmpty(d.read.claim)) s.AddClaim(PromptBuilder.Sanitize(d.read.claim, 16));
            s.lastThreat = d.threatE;

            Outcome forced = CheckForced(s);
            if (forced != null)
            {
                forced.decision = d;
                forced.requested = d.intentE;
                forced.notes.InsertRange(0, o.notes);
                FinishTurn(s, forced);
                return forced;
            }

            Intent i = Legalize(d.intentE, s, o.notes);
            i = Pacing(i, s, o.notes);
            o.intent = i;
            ApplyEffects(s, o);
            o.line = ChooseLine(s, i, d.line, o.notes);
            o.stage = StageFor(s, i);
            FinishTurn(s, o);
            return o;
        }

        // ---------------------------------------------------------------
        // 1) 把模型的"读"翻译成确定的数值变化（查表 + 性格修正）
        // ---------------------------------------------------------------
        static void ApplyRead(GameState s, Decision d, List<string> notes)
        {
            float dTrust = 0f, dSus = 0f, dPat = -3f, dFear = 0f;

            switch (d.toneE)
            {
                case Tone.Friendly: dTrust += 8f; dSus -= 4f; break;
                case Tone.Calm: dTrust += 3f; dSus -= 2f; break;
                case Tone.Pleading: dTrust += 2f; dSus += 3f; dPat -= 4f; break;
                case Tone.Angry: dTrust -= 8f; dSus += 10f; dFear += 6f; break;
                case Tone.Deceptive: dTrust -= 3f; dSus += 8f; break;
                case Tone.Nonsense: dSus += 5f; dPat -= 6f; break;
            }

            switch (d.threatE)
            {
                case Threat.Low: dSus += 6f; dFear += 8f; break;
                case Threat.High: dSus += 18f; dFear += 20f; break;
            }

            switch (s.temperament)
            {
                case Temperament.Lonely:
                    if (dTrust > 0f) dTrust *= 1.5f;
                    if (dPat < 0f) dPat *= 0.6f;
                    break;
                case Temperament.Paranoid:
                    if (dSus > 0f) dSus *= 1.6f;
                    if (dTrust > 0f) dTrust *= 0.6f;
                    break;
                default: // Predator：不怕威胁，不太在意谎言，对好感格外受用
                    if (dTrust > 0f) dTrust *= 1.3f;
                    if (d.toneE == Tone.Deceptive) dSus *= 0.5f;
                    dFear *= 0.3f;
                    if (dPat < 0f) dPat *= 0.8f;
                    break;
            }

            s.trust += dTrust;
            s.suspicion += dSus;
            s.patience += dPat;
            s.fear += dFear;
            s.Clamp();

            notes.Add(string.Format("读取 tone={0} threat={1} → 信任{2:+0.#;-0.#;0} 戒心{3:+0.#;-0.#;0} 耐心{4:+0.#;-0.#;0} 恐惧{5:+0.#;-0.#;0}",
                d.toneE, d.threatE, dTrust, dSus, dPat, dFear));
        }

        // ---------------------------------------------------------------
        // 2) 意图是否被状态允许，不允许就降级
        // ---------------------------------------------------------------
        static bool CanOpen(GameState s)
        {
            float bonus;
            if (s.temperament == Temperament.Predator) bonus = -12f;
            else if (s.temperament == Temperament.Paranoid) bonus = 10f;
            else bonus = -5f;
            float needTrust = (s.door == DoorState.Chain ? 45f : 58f) + bonus;
            float maxSus = s.door == DoorState.Chain ? 60f : 50f;
            return s.trust >= needTrust && s.suspicion <= maxSus;
        }

        static bool CanCrack(GameState s)
        {
            return s.trust >= 30f && s.suspicion <= 70f;
        }

        static Intent Legalize(Intent i, GameState s, List<string> notes)
        {
            if (i == Intent.Open)
            {
                if (CanOpen(s)) return Intent.Open;
                notes.Add("裁决：信任/戒心不满足开门条件，降级为开一条缝");
                i = Intent.Crack;
            }
            if (i == Intent.Crack)
            {
                if (s.door == DoorState.Chain)
                {
                    notes.Add("裁决：门已经挂链开着，改为追问");
                    return Intent.Ask;
                }
                if (CanCrack(s)) return Intent.Crack;
                notes.Add("裁决：不满足开缝条件，降级为拒绝");
                i = Intent.Refuse;
            }
            if (i == Intent.Police)
            {
                if (s.suspicion >= 65f || s.lastThreat == Threat.High) return Intent.Police;
                notes.Add("裁决：还没到报警的程度，降级为警告");
                i = Intent.Warn;
            }
            if (i == Intent.Slam)
            {
                if (s.patience <= 45f || s.suspicion >= 55f || s.fear >= 60f) return Intent.Slam;
                notes.Add("裁决：还没到关门的程度，降级为拒绝");
                i = Intent.Refuse;
            }
            return i;
        }

        // ---------------------------------------------------------------
        // 3) 节奏保护
        // ---------------------------------------------------------------
        static Intent Pacing(Intent i, GameState s, List<string> notes)
        {
            if (i == Intent.Silent && s.consecutiveSilent >= 2)
            {
                notes.Add("节奏：连续沉默过多，改为追问");
                i = Intent.Ask;
            }
            if (i == Intent.Withdraw && (s.turn < 2 || (s.hasLastIntent && s.lastIntent == Intent.Withdraw)))
            {
                notes.Add("节奏：现在不适合离开猫眼，改为沉默");
                i = Intent.Silent;
            }
            return i;
        }

        // ---------------------------------------------------------------
        // 4) 执行意图带来的确定性后果
        // ---------------------------------------------------------------
        static void ApplyEffects(GameState s, Outcome o)
        {
            switch (o.intent)
            {
                case Intent.Crack:
                    s.door = DoorState.Chain;
                    break;
                case Intent.Open:
                    s.door = DoorState.Open;
                    if (s.temperament == Temperament.Lonely) o.ending = EndingId.WelcomeLonely;
                    else if (s.temperament == Temperament.Paranoid) o.ending = EndingId.WelcomeParanoid;
                    else o.ending = EndingId.WelcomePredator;
                    break;
                case Intent.Slam:
                    s.door = DoorState.Closed;
                    s.slams++;
                    s.suspicion += 4f;
                    if (s.slams >= 3)
                    {
                        o.ending = EndingId.DrivenAway;
                        o.notes.Add("结局：被连续关门三次，彻底被赶走");
                    }
                    break;
                case Intent.Police:
                    o.ending = EndingId.Police;
                    break;
                case Intent.Refuse:
                    s.trust -= 1f;
                    break;
                case Intent.Warn:
                    s.suspicion += 2f;
                    break;
            }
            s.Clamp();
        }

        static void FinishTurn(GameState s, Outcome o)
        {
            s.turn++;
            if (o.intent == Intent.Silent) s.consecutiveSilent++;
            else s.consecutiveSilent = 0;
            s.lastIntent = o.intent;
            s.hasLastIntent = true;
            s.Clamp();
        }

        // ---------------------------------------------------------------
        // 台词：过滤、限长、去重，必要时用预设台词兜底
        // ---------------------------------------------------------------
        static string ChooseLine(GameState s, Intent i, string raw, List<string> notes)
        {
            if (i == Intent.Silent || i == Intent.Withdraw) return "";

            string line = PromptBuilder.Sanitize(raw ?? "", 80);
            bool bad = line.Length == 0;
            if (!bad)
            {
                for (int k = 0; k < BadWords.Length; k++)
                {
                    if (line.Contains(BadWords[k]))
                    {
                        bad = true;
                        notes.Add("过滤：台词含出戏词");
                        break;
                    }
                }
            }
            if (!bad && s.recentLines.Contains(line))
            {
                bad = true;
                notes.Add("过滤：台词与最近重复");
            }
            if (bad)
            {
                line = FallbackLine(s, i);
                for (int k = 0; k < 4 && s.recentLines.Contains(line); k++) line = FallbackLine(s, i);
                notes.Add("使用预设台词");
            }

            line = TrimLine(line, 30);
            s.RememberLine(line);
            return line;
        }

        static string TrimLine(string l, int max)
        {
            if (l.Length <= max) return l;
            int cut = -1;
            for (int k = max - 1; k >= 6; k--)
            {
                if ("，。？！、,.?!…".IndexOf(l[k]) >= 0) { cut = k; break; }
            }
            if (cut >= 0) return l.Substring(0, cut + 1);
            return l.Substring(0, max) + "……";
        }

        // ---------------------------------------------------------------
        // 预设台词 / 舞台提示（固定的表现，随机只用于挑选）
        // ---------------------------------------------------------------
        static string[] Join(string[] a, string[] b)
        {
            string[] r = new string[a.Length + b.Length];
            Array.Copy(a, r, a.Length);
            Array.Copy(b, 0, r, a.Length, b.Length);
            return r;
        }

        static string[] Lines(Temperament t, Intent i)
        {
            switch (i)
            {
                case Intent.Ask:
                    {
                        string[] baseLines = { "你找谁？", "这么晚了，有什么事？", "你是怎么上来的？", "你叫什么名字？" };
                        if (t == Temperament.Lonely) return Join(baseLines, new[] { "……你是一个人来的吗？", "你为什么要敲我的门？" });
                        if (t == Temperament.Paranoid) return Join(baseLines, new[] { "谁让你来的？", "你刚才说的，再说一遍。" });
                        return Join(baseLines, new[] { "别站在那儿，你冷不冷？", "你从哪儿来的？远吗？" });
                    }
                case Intent.Refuse:
                    {
                        string[] baseLines = { "我不认识你。", "不方便。", "请回去吧。" };
                        if (t == Temperament.Lonely) return Join(baseLines, new[] { "……今天不行。", "你还是走吧。" });
                        if (t == Temperament.Paranoid) return Join(baseLines, new[] { "我不会开门的。", "我说了，不行。" });
                        return Join(baseLines, new[] { "再想想，你真的要走吗？", "别急着走。" });
                    }
                case Intent.Warn:
                    return new[] { "再不走，我就叫人了。", "离门远一点。", "我在看着你。" };
                case Intent.Crack:
                    return new[] { "……说吧。", "就这样说。", "只开这么一点。" };
                case Intent.Open:
                    return new[] { "进来吧。", "……进来。别出声。", "进来，快。" };
                case Intent.Slam:
                    return new[] { "别再敲了。", "走。", "别来烦我。" };
                case Intent.Police:
                    return new[] { "我已经报警了。", "你等着。" };
                default:
                    return new[] { "" };
            }
        }

        public static string FallbackLine(GameState s, Intent i)
        {
            string[] arr = Lines(s.temperament, i);
            return arr[s.rng.Next(arr.Length)];
        }

        static readonly string[] SilentStage =
        {
            "（他没有回答。猫眼里的眼睛一眨不眨。）",
            "（门后只有呼吸声。）",
            "（你听见门内有什么东西被轻轻挪开了。）",
            "（很久，没有任何声音。）"
        };

        public static string StageFor(GameState s, Intent i)
        {
            switch (i)
            {
                case Intent.Silent: return SilentStage[s.rng.Next(SilentStage.Length)];
                case Intent.Withdraw: return "（猫眼暗了。脚步声在门后远去。）";
                case Intent.Crack: return "（锁链哗啦一响，门开了一道缝。）";
                case Intent.Open: return "（锁，一道一道地打开了。）";
                case Intent.Slam: return "（门被猛地关上。）";
                case Intent.Police: return "（你听见门后传来拨号的声音。）";
                default: return "";
            }
        }
    }
}

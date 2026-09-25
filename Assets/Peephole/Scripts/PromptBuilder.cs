using System.Text;

namespace Peephole
{
    /// <summary>
    /// 情境构建器：代码写句子，模型读句子。
    /// 每回合从零重建提示词，不依赖聊天历史；静态设定放 system，动态状态放 user。
    /// </summary>
    public static class PromptBuilder
    {
        // ---------- 语法约束（GBNF）：模型只能输出这个结构，枚举只能取封闭集合 ----------
        static string Lit(string s)
        {
            // 生成 GBNF 字面量：  "\"s\""  （即 JSON 里带引号的字符串）
            return "\"\\\"" + s + "\\\"\"";
        }

        static string Alt(params string[] values)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(Lit(values[i]));
            }
            return sb.ToString();
        }

        static string BuildGbnf()
        {
            StringBuilder g = new StringBuilder();
            g.Append("root ::= \"{\" ws " + Lit("read") + " ws \":\" ws readobj ws \",\" ws " + Lit("intent") + " ws \":\" ws intent ws \",\" ws " + Lit("line") + " ws \":\" ws line ws \"}\"\n");
            g.Append("readobj ::= \"{\" ws " + Lit("tone") + " ws \":\" ws tone ws \",\" ws " + Lit("threat") + " ws \":\" ws threat ws \",\" ws " + Lit("claim") + " ws \":\" ws claim ws \"}\"\n");
            g.Append("tone ::= " + Alt("calm", "friendly", "pleading", "angry", "deceptive", "nonsense") + "\n");
            g.Append("threat ::= " + Alt("none", "low", "high") + "\n");
            g.Append("intent ::= " + Alt("silent", "ask", "refuse", "warn", "crack", "open", "slam", "police", "withdraw") + "\n");
            g.Append("claim ::= \"\\\"\" ch{0,16} \"\\\"\"\n");
            g.Append("line ::= \"\\\"\" ch{0,40} \"\\\"\"\n");
            g.Append("ch ::= [^\"\\\\\\x00-\\x1F\\x7F]\n");
            g.Append("ws ::= \" \"?\n");
            return g.ToString();
        }

        public static readonly string Gbnf = BuildGbnf();

        // ---------- 静态设定 ----------
        public static string Persona(Temperament t)
        {
            switch (t)
            {
                case Temperament.Lonely:
                    return "你一个人住了很久，太久没有人和你说过话。你嘴上冷淡，心里其实渴望有人留下来陪你，但害怕被拒绝，所以从不直说。";
                case Temperament.Paranoid:
                    return "你极度多疑，觉得每一个来敲门的人都有目的。你会追问细节，抓对方话里的矛盾，从不轻易相信任何人。";
                default:
                    return "你表面礼貌、温和、有耐心，其实在挑选猎物。你会让对方放松警惕，引导对方自己走进屋里。你绝不能露出破绽，也不能直接说出你的真实意图。";
            }
        }

        public static string BuildSystem(Temperament t)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("你住在302室，此刻正隔着猫眼，看着门外一个想进来的陌生人。你说话很短，冷淡，克制，像隔着一扇门在说话。\n");
            sb.Append(Persona(t)).Append('\n');
            sb.Append("你要读懂门外这个人，然后决定下一步做什么。你只输出一个JSON对象，不要输出任何别的内容，格式：\n");
            sb.Append("{\"read\":{\"tone\":\"...\",\"threat\":\"...\",\"claim\":\"...\"},\"intent\":\"...\",\"line\":\"...\"}\n");
            sb.Append("read 是你对门外这个人的判断：\n");
            sb.Append("tone（对方说话的语气）：calm平静 / friendly友好 / pleading恳求 / angry愤怒 / deceptive可疑 / nonsense胡言乱语\n");
            sb.Append("threat（对方带来的威胁）：none没有 / low有一点 / high很大\n");
            sb.Append("claim（对方声称的事实，16字以内，没有就留空）\n");
            sb.Append("intent 是你接下来的动作：\n");
            sb.Append("silent 什么都不说，只是盯着他\n");
            sb.Append("ask 追问一个具体的问题\n");
            sb.Append("refuse 拒绝他\n");
            sb.Append("warn 警告他离开\n");
            sb.Append("crack 挂着链子把门开一条缝\n");
            sb.Append("open 打开门让他进来\n");
            sb.Append("slam 把门关死\n");
            sb.Append("police 报警\n");
            sb.Append("withdraw 离开猫眼一会儿\n");
            sb.Append("line 是你说的话，20字以内，口语，不用感叹号；intent 为 silent 或 withdraw 时 line 留空。\n");
            sb.Append("判断依据：戒心高、耐心少、对方威胁大时，倾向 refuse / warn / slam / police；只有信任高、戒心低时，才可能 crack / open。对方前后说法矛盾时，要追问，并提高戒心。\n");
            sb.Append("不要提到“模型”“AI”“规则”“JSON”，不要解释你的想法，不要透露你内心真正的意图。");
            return sb.ToString();
        }

        // ---------- 每回合的动态情境 ----------
        public static string BuildUser(GameState s, string playerText)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("【你的状态】戒心：").Append(GameState.Level(s.suspicion));
            sb.Append("；耐心：").Append(GameState.PatienceWord(s.patience));
            sb.Append("；恐惧：").Append(GameState.Level(s.fear));
            sb.Append("；信任：").Append(GameState.Level(s.trust)).Append('\n');

            sb.Append("【门】").Append(s.DoorWord()).Append("；对方").Append(s.DistanceWord()).Append('\n');

            sb.Append("【对方此前声称过】");
            sb.Append(s.claims.Count == 0 ? "无" : string.Join("；", s.claims.ToArray())).Append('\n');

            sb.Append("【刚才发生的事】");
            System.Collections.Generic.List<string> evs = new System.Collections.Generic.List<string>(s.events);
            if (s.knocks > 0) evs.Insert(0, "他敲了" + s.knocks + "次门");
            sb.Append(evs.Count == 0 ? "无" : string.Join("。", evs.ToArray())).Append('\n');

            sb.Append("【对方现在对你说】“").Append(Sanitize(playerText, 120)).Append("”\n");
            sb.Append("请只输出JSON。");
            return sb.ToString();
        }

        /// <summary>清理玩家输入：去掉控制字符和引号，限制长度。玩家的话永远只是数据。</summary>
        public static string Sanitize(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < ' ') { sb.Append(' '); continue; }
                if (c == '"' || c == '“' || c == '”') { sb.Append('\''); continue; }
                sb.Append(c);
            }
            string r = sb.ToString().Trim();
            if (r.Length > max) r = r.Substring(0, max);
            return r;
        }
    }
}

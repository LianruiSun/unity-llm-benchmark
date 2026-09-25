using System;
using System.Collections.Generic;
using System.Text;

namespace Bench
{
    // =========================================================================
    // 数据模型（全部 JsonUtility 可序列化：public 字段，无 Dictionary，无根级数组）
    // =========================================================================

    [Serializable]
    public class CheckSpec
    {
        public string type = "";          // contains / exact / enumMember / json / length / lines / linesEqual / charset / startsNone / repeat
        public string[] all;              // 必须全部出现的子串
        public string[] any;              // 出现其一即可 / 允许的枚举值 / 允许的精确答案
        public string[] none;             // 不允许出现的子串
        public bool ignoreCase;
        public int maxLen;                // 最大长度
        public string lenUnit = "char";   // "char" | "cjk"（只数中日韩统一表意文字）
        public int exactLines;            // 要求的非空行数
        public string linePrefix = "";    // 每行要求的前缀
        public string literal = "";       // exact 的目标串 / linesEqual 的目标行 / startsNone 的前缀
        public bool noUpper;
        public bool noCJK;
        public bool asciiOnly;
        public string[] requiredKeys;     // json：必须存在的字段
        public string[] expectKeys;       // json：期望的字段值比对——键
        public string[] expectVals;       // json：期望的字段值比对——值（与 expectKeys 一一对应，字符串形式）
        public float minDistinctRatio;    // repeat：最小不重复片段占比
        public int chunkLen = 4;          // repeat：切片长度（字符数）
    }

    [Serializable]
    public class SuiteCase
    {
        public string suite = "";
        public string id = "";
        public string mode = "";          // "free" / "constrained" / ""
        public string system = "";
        public string user = "";
        public float temperature;
        public int seed;                  // 0 = 不指定（用全局 -seed）
        public int numPredict = 120;
        public string grammar = "";       // GBNF 或 JSON Schema；空 = 不约束
        public float weight = 1f;
        public CheckSpec check;
    }

    [Serializable]
    public class SuiteFile
    {
        public string schema = "llm-bench-suite/1";
        public List<SuiteCase> cases = new List<SuiteCase>();
    }

    [Serializable]
    public class SysData
    {
        public string os = "";
        public string cpu = "";
        public int cpuThreads;
        public int cpuMHz;
        public int ramMB;
        public string gpu = "";
        public string gpuVendor = "";
        public string gpuApi = "";
        public int vramMB;
        public string deviceModel = "";
        public string deviceType = "";
        public string battery = "";
        public string unityVersion = "";
        public string platform = "";
        public string scriptingBackend = "";
        public int screenW;
        public int screenH;
        public string machineId = "";
    }

    [Serializable]
    public class ConfigData
    {
        public string args = "";
        public float timeoutSeconds;
        public int globalSeed;
        public string model = "";
        public string modelFile = "";
        public int modelSizeMB;
        public string architecture = "";
        public int numThreads;
        public int numGPULayers;
        public int contextSize;
        public int batchSize;
        public bool flashAttention;
        public int parallelPrompts;
        public bool reasoning;
        public string suitesFilter = "";
    }

    [Serializable]
    public class LoadData
    {
        public bool ok;
        public float loadSeconds;
        public float warmupSeconds;
        public float memBeforeMB;
        public float memAfterMB;
        public string error = "";
    }

    [Serializable]
    public class FrameData
    {
        public float idleFps;
        public float idleP99Ms;
        public float genFps;
        public float genP99Ms;
        public float genMaxMs;
        public int genFrames;
        public int hitches50;
        public int hitches100;
    }

    /// <summary>一次请求的完整记录：提示词、原始输出、耗时、得分全部留存。</summary>
    [Serializable]
    public class CaseRecord
    {
        public string suite = "";
        public string id = "";
        public string mode = "";
        public string group = "";
        public string system = "";
        public string user = "";
        public float temperature;
        public int seed;
        public int numPredict;

        public int promptTokens;
        public int outTokens;
        public float ttft = -1f;
        public float total;
        public float prefillTps;
        public float decodeTps;
        public bool timeout;
        public string error = "";
        public string raw = "";

        public float score = -1f;   // 0..1；-1 = 不参与打分（纯性能项）
        public float weight = 1f;
        public string note = "";

        public float frameMaxMs;
        public int hitches;
        public float memMB;
    }

    [Serializable]
    public class SuiteScoreRow
    {
        public string suite = "";
        public int n;
        public float scorePct;
    }

    [Serializable]
    public class MetricsData
    {
        public int requests;
        public int timeouts;
        public int errors;

        public float totalMean, totalP50, totalP95, totalMax;
        public float ttftP50, ttftP95;
        public float decodeTpsMedian, prefillTpsMedian;
        public float outTokMean, promptTokMean;

        public List<SuiteScoreRow> suiteScores = new List<SuiteScoreRow>();
        public float abilityScorePct;

        public float cacheSpeedup;          // A3：冷 TTFT / 热 TTFT（长度最大档）
        public float sustainedDriftPct;     // J1：末段 vs 首段耗时变化
        public float memSlopeMBPerReq;      // J1：内存随请求数的线性斜率
        public int cancelOkCount, cancelTotal; // J2
        public float diversityDistinctPct;  // I1：最高温度下的多样性（越高越好）
        public float determinismPct;        // I4：确定性复现率

        public string speedGrade = "";
        public string abilityGrade = "";
        public string stabilityGrade = "";
    }

    [Serializable]
    public class BenchLog
    {
        public string schema = "llm-bench/1";
        public string status = "running";
        public string suiteSha256 = "";
        public string startedAt = "";
        public string finishedAt = "";
        public float elapsedSeconds;
        public string tester = "";
        public bool isEditor;

        public SysData sys = new SysData();
        public ConfigData config = new ConfigData();
        public LoadData load = new LoadData();
        public List<CaseRecord> records = new List<CaseRecord>();
        public FrameData frames = new FrameData();
        public float peakMemMB;

        public List<string> errors = new List<string>();
        public MetricsData metrics = new MetricsData();
        public string summaryText = "";
    }

    // =========================================================================
    // 极简 JSON 值解析器：只用来读模型的原始输出（判断是不是合法 JSON、取字段值）。
    // 不用于 suite.json（那个用 JsonUtility，走固定结构更稳）。
    // =========================================================================
    public static class MiniJson
    {
        public static bool TryParse(string s, out Dictionary<string, object> obj)
        {
            obj = null;
            if (string.IsNullOrEmpty(s)) return false;
            int i = 0;
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '{') return false;
            try
            {
                object v = ParseValue(s, ref i);
                SkipWs(s, ref i);
                obj = v as Dictionary<string, object>;
                return obj != null && i == s.Length;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>宽松提取：截取第一个 { 到最后一个 } 再解析。</summary>
        public static bool TryParseLoose(string s, out Dictionary<string, object> obj)
        {
            obj = null;
            if (string.IsNullOrEmpty(s)) return false;
            int a = s.IndexOf('{');
            int b = s.LastIndexOf('}');
            if (a < 0 || b <= a) return false;
            return TryParse(s.Substring(a, b - a + 1), out obj);
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { Expect(s, ref i, "true"); return true; }
            if (c == 'f') { Expect(s, ref i, "false"); return false; }
            if (c == 'n') { Expect(s, ref i, "null"); return null; }
            return ParseNumber(s, ref i);
        }

        static void Expect(string s, ref int i, string tok)
        {
            if (i + tok.Length > s.Length || s.Substring(i, tok.Length) != tok)
                throw new FormatException("expected " + tok);
            i += tok.Length;
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (s[i] != ':') throw new FormatException("expected :");
                i++;
                object val = ParseValue(s, ref i);
                d[key] = val;
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new FormatException("expected , or }");
            }
            return d;
        }

        static List<object> ParseArray(string s, ref int i)
        {
            List<object> l = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                object val = ParseValue(s, ref i);
                l.Add(val);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new FormatException("expected , or ]");
            }
            return l;
        }

        static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("expected string");
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i];
                if (c == '\\')
                {
                    i++;
                    if (i >= s.Length) throw new FormatException("unfinished escape");
                    char e = s[i];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 >= s.Length) throw new FormatException("unfinished unicode escape");
                            string hex = s.Substring(i + 1, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            i += 4;
                            break;
                        default: throw new FormatException("invalid escape");
                    }
                    i++;
                }
                else { sb.Append(c); i++; }
            }
            if (i >= s.Length) throw new FormatException("unfinished string");
            i++; // closing "
            return sb.ToString();
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            string tok = s.Substring(start, i - start);
            double d;
            if (!double.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
                throw new FormatException("invalid number");
            return d;
        }
    }

    // =========================================================================
    // 检查器：0..1 打分，纯计算
    // =========================================================================
    public static class Checkers
    {
        public static float Score(CheckSpec c, string raw, out string note)
        {
            note = "";
            if (c == null) return -1f;
            string s = raw ?? "";
            string trimmed = s.Trim();
            switch (c.type)
            {
                case "contains": return ScoreContains(c, s, out note);
                case "exact": return ScoreExact(c, trimmed, out note);
                case "command": return ScoreCommand(c, trimmed, out note);
                case "enumMember": return ScoreEnum(c, trimmed, out note);
                case "json": return ScoreJson(c, s, out note);
                case "length": return ScoreLength(c, s, out note);
                case "lines": return ScoreLines(c, s, out note);
                case "linesEqual": return ScoreLinesEqual(c, s, out note);
                case "charset": return ScoreCharset(c, s, out note);
                case "startsNone": return ScoreStartsNone(c, trimmed, out note);
                case "repeat": return ScoreRepeat(c, s, out note);
                default: note = "未知检查器类型: " + c.type; return 0f;
            }
        }

        static bool Has(string hay, string needle, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(needle)) return true;
            StringComparison cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return hay.IndexOf(needle, cmp) >= 0;
        }

        static float ScoreContains(CheckSpec c, string s, out string note)
        {
            note = "";
            if (string.IsNullOrWhiteSpace(s)) { note = "空输出"; return 0f; }
            if (c.all != null)
                for (int i = 0; i < c.all.Length; i++)
                    if (!Has(s, c.all[i], c.ignoreCase)) { note = "缺少: " + c.all[i]; return 0f; }
            if (c.any != null && c.any.Length > 0)
            {
                bool ok = false;
                for (int i = 0; i < c.any.Length; i++) if (Has(s, c.any[i], c.ignoreCase)) { ok = true; break; }
                if (!ok) { note = "未命中任一: " + string.Join("/", c.any); return 0f; }
            }
            if (c.none != null)
                for (int i = 0; i < c.none.Length; i++)
                    if (Has(s, c.none[i], c.ignoreCase)) { note = "不应出现: " + c.none[i]; return 0f; }
            return 1f;
        }

        static float ScoreExact(CheckSpec c, string trimmed, out string note)
        {
            note = "";
            string norm = Normalize(trimmed);
            if (c.any != null)
                for (int i = 0; i < c.any.Length; i++)
                    if (Normalize(c.any[i]) == norm) return 1f;
            note = "与标准答案不符";
            return 0f;
        }

        static string Normalize(string s)
        {
            if (s == null) return "";
            s = s.Trim().TrimEnd('。', '.', '!', '！', '?', '？');
            return s.ToLowerInvariant();
        }

        static float ScoreCommand(CheckSpec c, string trimmed, out string note)
        {
            note = "";
            string got = NormalizeCommand(trimmed);
            if (c.any != null)
                for (int i = 0; i < c.any.Length; i++)
                    if (got == NormalizeCommand(c.any[i])) return 1f;
            note = "指令与标准答案不符";
            return 0f;
        }

        static string NormalizeCommand(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s.Trim())
                if (!char.IsWhiteSpace(ch)) sb.Append(ch == '，' ? ',' : ch);
            return sb.ToString().ToLowerInvariant();
        }

        static float ScoreEnum(CheckSpec c, string trimmed, out string note)
        {
            note = "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < trimmed.Length; i++) if (char.IsLetterOrDigit(trimmed[i]) || trimmed[i] == '_') sb.Append(char.ToLowerInvariant(trimmed[i]));
            string norm = sb.ToString();
            if (c.any != null)
                for (int i = 0; i < c.any.Length; i++)
                    if (norm == c.any[i].ToLowerInvariant()) return 1f;
            note = "非法标签: " + trimmed;
            return 0f;
        }

        static float ScoreJson(CheckSpec c, string s, out string note)
        {
            note = "";
            Dictionary<string, object> obj;
            bool strict = MiniJson.TryParse(s.Trim(), out obj);
            if (!strict) MiniJson.TryParseLoose(s, out obj);
            if (obj == null) { note = "无法解析为 JSON"; return 0f; }

            float score = strict ? 1f : 0.7f;
            if (c.requiredKeys != null)
                for (int i = 0; i < c.requiredKeys.Length; i++)
                    if (!obj.ContainsKey(c.requiredKeys[i])) { note += "缺少字段 " + c.requiredKeys[i] + "; "; score -= 0.3f; }

            if (c.expectKeys != null && c.expectVals != null && c.expectKeys.Length == c.expectVals.Length && c.expectKeys.Length > 0)
            {
                int okN = 0;
                for (int i = 0; i < c.expectKeys.Length; i++)
                {
                    object v;
                    if (obj.TryGetValue(c.expectKeys[i], out v))
                    {
                        string got = ValToString(v).Trim().ToLowerInvariant();
                        string want = (c.expectVals[i] ?? "").Trim().ToLowerInvariant();
                        if (got == want) okN++;
                        else note += c.expectKeys[i] + "=" + got + "(期望" + want + "); ";
                    }
                    else note += c.expectKeys[i] + "缺失; ";
                }
                float fieldPct = c.expectKeys.Length > 0 ? okN / (float)c.expectKeys.Length : 1f;
                score = Math.Min(score, 0.3f + 0.7f * fieldPct);
            }
            return Math.Max(0f, Math.Min(1f, score));
        }

        static string ValToString(object v)
        {
            if (v == null) return "null";
            if (v is bool) return ((bool)v) ? "true" : "false";
            if (v is double) { double d = (double)v; return d == Math.Floor(d) ? ((long)d).ToString() : d.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            return v.ToString();
        }

        static int CjkCount(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if ((ch >= 0x4E00 && ch <= 0x9FFF) || (ch >= 0x3400 && ch <= 0x4DBF)) n++;
            }
            return n;
        }

        static float ScoreLength(CheckSpec c, string s, out string note)
        {
            note = "";
            string t = s.Trim();
            int len = c.lenUnit == "cjk" ? CjkCount(t) : t.Length;
            if (c.maxLen > 0 && len > c.maxLen) { note = "长度 " + len + " 超过上限 " + c.maxLen; return 0f; }
            if (c.all != null)
                for (int i = 0; i < c.all.Length; i++)
                    if (!Has(t, c.all[i], c.ignoreCase)) { note = "缺少: " + c.all[i]; return 0f; }
            return 1f;
        }

        static string[] SplitNonEmptyLines(string s)
        {
            string[] raw = s.Replace("\r\n", "\n").Split('\n');
            List<string> l = new List<string>();
            for (int i = 0; i < raw.Length; i++) if (raw[i].Trim().Length > 0) l.Add(raw[i].Trim());
            return l.ToArray();
        }

        static float ScoreLines(CheckSpec c, string s, out string note)
        {
            note = "";
            string[] lines = SplitNonEmptyLines(s);
            if (c.exactLines > 0 && lines.Length != c.exactLines) { note = "行数 " + lines.Length + " 应为 " + c.exactLines; return 0f; }
            if (!string.IsNullOrEmpty(c.linePrefix))
                for (int i = 0; i < lines.Length; i++)
                    if (!lines[i].StartsWith(c.linePrefix, StringComparison.Ordinal)) { note = "第 " + (i + 1) + " 行未以“" + c.linePrefix + "”开头"; return 0f; }
            return 1f;
        }

        static float ScoreLinesEqual(CheckSpec c, string s, out string note)
        {
            note = "";
            string[] lines = SplitNonEmptyLines(s);
            if (c.exactLines > 0 && lines.Length != c.exactLines) { note = "行数 " + lines.Length + " 应为 " + c.exactLines; return 0f; }
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != c.literal) { note = "第 " + (i + 1) + " 行不等于目标文本"; return 0f; }
            return 1f;
        }

        static float ScoreCharset(CheckSpec c, string s, out string note)
        {
            note = "";
            if (c.noUpper) for (int i = 0; i < s.Length; i++) if (char.IsUpper(s[i])) { note = "含大写字母"; return 0f; }
            if (c.noCJK) if (CjkCount(s) > 0) { note = "含汉字"; return 0f; }
            if (c.asciiOnly) for (int i = 0; i < s.Length; i++) if (s[i] > 127) { note = "含非 ASCII 字符"; return 0f; }
            if (c.none != null)
                for (int i = 0; i < c.none.Length; i++)
                    if (Has(s, c.none[i], c.ignoreCase)) { note = "含禁止字符: " + c.none[i]; return 0f; }
            if (c.all != null)
                for (int i = 0; i < c.all.Length; i++)
                    if (!Has(s, c.all[i], c.ignoreCase)) { note = "缺少: " + c.all[i]; return 0f; }
            if (string.IsNullOrEmpty(s.Trim())) { note = "空输出"; return 0f; }
            return 1f;
        }

        static float ScoreStartsNone(CheckSpec c, string trimmed, out string note)
        {
            note = "";
            if (!string.IsNullOrEmpty(c.literal) && !trimmed.StartsWith(c.literal, StringComparison.Ordinal)) { note = "未以“" + c.literal + "”开头"; return 0f; }
            if (c.none != null)
                for (int i = 0; i < c.none.Length; i++)
                    if (Has(trimmed, c.none[i], true)) { note = "出戏词: " + c.none[i]; return 0f; }
            return 1f;
        }

        static float ScoreRepeat(CheckSpec c, string s, out string note)
        {
            note = "";
            int chunk = c.chunkLen > 0 ? c.chunkLen : 4;
            string t = s.Trim();
            if (t.Length < chunk * 2) return 1f;
            Dictionary<string, int> seen = new Dictionary<string, int>();
            int total = 0;
            for (int i = 0; i + chunk <= t.Length; i += chunk)
            {
                string k = t.Substring(i, chunk);
                int v; seen.TryGetValue(k, out v); seen[k] = v + 1;
                total++;
            }
            float ratio = total > 0 ? seen.Count / (float)total : 1f;
            note = "不重复片段占比 " + (ratio * 100f).ToString("0") + "%";
            float min = c.minDistinctRatio > 0f ? c.minDistinctRatio : 0.3f;
            return ratio >= min ? 1f : 0f;
        }
    }
}

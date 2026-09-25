using System;
using UnityEngine;

namespace Peephole
{
    [Serializable]
    public class ReadPart
    {
        public string tone;
        public string threat;
        public string claim;
    }

    /// <summary>
    /// 模型（或 Mock）给出的"提议"。它只是提议，最终发生什么由 Arbiter 决定。
    /// 前三个字段直接对应 JSON，其余是解析后的强类型结果与调试信息。
    /// </summary>
    [Serializable]
    public class Decision
    {
        public ReadPart read;
        public string intent;
        public string line;

        [NonSerialized] public Tone toneE;
        [NonSerialized] public Threat threatE;
        [NonSerialized] public Intent intentE;
        [NonSerialized] public string source = "";
        [NonSerialized] public string raw = "";
        [NonSerialized] public float latency;

        public static Decision Make(Tone tone, Threat threat, string claim, Intent intent, string line, string source)
        {
            Decision d = new Decision();
            d.read = new ReadPart();
            d.read.tone = tone.ToString().ToLowerInvariant();
            d.read.threat = threat.ToString().ToLowerInvariant();
            d.read.claim = claim ?? "";
            d.intent = intent.ToString().ToLowerInvariant();
            d.line = line ?? "";
            d.toneE = tone;
            d.threatE = threat;
            d.intentE = intent;
            d.source = source;
            d.raw = "";
            return d;
        }

        /// <summary>从模型的原始输出里解析出 Decision。任何一步不合法都返回 false。</summary>
        public static bool TryParse(string raw, out Decision d, out string error)
        {
            d = null;
            error = null;
            if (string.IsNullOrEmpty(raw)) { error = "输出为空"; return false; }

            int a = raw.IndexOf('{');
            int b = raw.LastIndexOf('}');
            if (a < 0 || b <= a) { error = "找不到 JSON"; return false; }

            string json = raw.Substring(a, b - a + 1);
            Decision parsed;
            try
            {
                parsed = JsonUtility.FromJson<Decision>(json);
            }
            catch (Exception e)
            {
                error = "JSON 解析失败：" + e.Message;
                return false;
            }
            if (parsed == null || parsed.read == null) { error = "缺少 read 字段"; return false; }

            Tone t;
            Threat th;
            Intent it;
            if (!Enum.TryParse<Tone>(parsed.read.tone ?? "", true, out t) || !Enum.IsDefined(typeof(Tone), t))
            { error = "tone 不合法：" + parsed.read.tone; return false; }
            if (!Enum.TryParse<Threat>(parsed.read.threat ?? "", true, out th) || !Enum.IsDefined(typeof(Threat), th))
            { error = "threat 不合法：" + parsed.read.threat; return false; }
            if (!Enum.TryParse<Intent>(parsed.intent ?? "", true, out it) || !Enum.IsDefined(typeof(Intent), it))
            { error = "intent 不合法：" + parsed.intent; return false; }

            parsed.toneE = t;
            parsed.threatE = th;
            parsed.intentE = it;
            if (parsed.read.claim == null) parsed.read.claim = "";
            if (parsed.line == null) parsed.line = "";
            parsed.raw = raw;
            d = parsed;
            return true;
        }
    }
}

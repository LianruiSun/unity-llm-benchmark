using System;
using System.Collections.Generic;
using System.Text;

namespace Bench
{
    /// <summary>把原始记录汇总成指标和一份人能直接读的文字报告。纯计算，不依赖 Unity。</summary>
    public static class BenchAnalyzer
    {
        static readonly string[] AbilitySuites = { "B", "C", "D", "E", "H" };
        static readonly Dictionary<string, float> AbilityWeight = new Dictionary<string, float>
        {
            { "B", 0.25f }, { "C", 0.2f }, { "D", 0.3f }, { "E", 0.15f }, { "H", 0.1f }
        };

        public static float Percentile(List<float> v, float p)
        {
            if (v == null || v.Count == 0) return 0f;
            List<float> s = new List<float>(v);
            s.Sort();
            if (s.Count == 1) return s[0];
            float pos = (s.Count - 1) * p;
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);
            if (lo == hi) return s[lo];
            return s[lo] + (s[hi] - s[lo]) * (pos - lo);
        }

        public static float Mean(List<float> v)
        {
            if (v == null || v.Count == 0) return 0f;
            float t = 0f;
            for (int i = 0; i < v.Count; i++) t += v[i];
            return t / v.Count;
        }

        static string F(float v, int digits) { return v.ToString("F" + digits, System.Globalization.CultureInfo.InvariantCulture); }

        public static void Compute(BenchLog log)
        {
            MetricsData m = new MetricsData();
            List<CaseRecord> all = log.records;
            m.requests = all.Count;

            List<float> totals = new List<float>();
            List<float> ttfts = new List<float>();
            List<float> dec = new List<float>();
            List<float> pre = new List<float>();
            List<float> outTok = new List<float>();
            List<float> promptTok = new List<float>();

            for (int i = 0; i < all.Count; i++)
            {
                CaseRecord r = all[i];
                if (r.timeout) m.timeouts++;
                if (!string.IsNullOrEmpty(r.error)) m.errors++;
                if (r.timeout || !string.IsNullOrEmpty(r.error)) continue;
                totals.Add(r.total);
                if (r.ttft >= 0f) ttfts.Add(r.ttft);
                if (r.decodeTps > 0f) dec.Add(r.decodeTps);
                if (r.prefillTps > 0f) pre.Add(r.prefillTps);
                if (r.outTokens > 0) outTok.Add(r.outTokens);
                if (r.promptTokens > 0) promptTok.Add(r.promptTokens);
            }
            m.totalMean = Mean(totals);
            m.totalP50 = Percentile(totals, 0.5f);
            m.totalP95 = Percentile(totals, 0.95f);
            m.totalMax = Percentile(totals, 1f);
            m.ttftP50 = Percentile(ttfts, 0.5f);
            m.ttftP95 = Percentile(ttfts, 0.95f);
            m.decodeTpsMedian = Percentile(dec, 0.5f);
            m.prefillTpsMedian = Percentile(pre, 0.5f);
            m.outTokMean = Mean(outTok);
            m.promptTokMean = Mean(promptTok);

            // ---- 每个套件的得分（按权重）----
            Dictionary<string, float> sumScore = new Dictionary<string, float>();
            Dictionary<string, float> sumWeight = new Dictionary<string, float>();
            Dictionary<string, int> countN = new Dictionary<string, int>();
            for (int i = 0; i < all.Count; i++)
            {
                CaseRecord r = all[i];
                if (r.score < 0f) continue;
                float w = r.weight > 0f ? r.weight : 1f;
                float sv, wv; int nv;
                sumScore.TryGetValue(r.suite, out sv); sumScore[r.suite] = sv + r.score * w;
                sumWeight.TryGetValue(r.suite, out wv); sumWeight[r.suite] = wv + w;
                countN.TryGetValue(r.suite, out nv); countN[r.suite] = nv + 1;
            }
            List<string> suites = new List<string>(sumScore.Keys);
            suites.Sort();
            for (int i = 0; i < suites.Count; i++)
            {
                string su = suites[i];
                SuiteScoreRow row = new SuiteScoreRow();
                row.suite = su;
                row.n = countN[su];
                row.scorePct = sumWeight[su] > 0f ? 100f * sumScore[su] / sumWeight[su] : 0f;
                m.suiteScores.Add(row);
            }

            float abilitySum = 0f, abilityWeightSum = 0f;
            for (int i = 0; i < AbilitySuites.Length; i++)
            {
                string su = AbilitySuites[i];
                if (!sumWeight.ContainsKey(su) || sumWeight[su] <= 0f) continue;
                float pct = 100f * sumScore[su] / sumWeight[su];
                float w = AbilityWeight[su];
                abilitySum += pct * w;
                abilityWeightSum += w;
            }
            m.abilityScorePct = abilityWeightSum > 0f ? abilitySum / abilityWeightSum : 0f;

            m.speedGrade = GradeSpeed(m);
            m.abilityGrade = GradeAbility(m);
            m.stabilityGrade = GradeStability(m);

            log.metrics = m;
            log.summaryText = BuildText(log);
        }

        static string GradeSpeed(MetricsData m)
        {
            if (m.decodeTpsMedian <= 0f) return "无数据";
            float tps = m.decodeTpsMedian;
            float ttft = m.ttftP50 > 0f ? m.ttftP50 : 99f;
            if (tps >= 20f && ttft <= 1.5f) return "流畅";
            if (tps >= 10f && ttft <= 3f) return "可用";
            if (tps >= 5f && ttft <= 6f) return "勉强";
            return "不适合实时";
        }

        static string GradeAbility(MetricsData m)
        {
            if (m.abilityScorePct <= 0f && m.suiteScores.Count == 0) return "无数据";
            if (m.abilityScorePct >= 85f) return "优";
            if (m.abilityScorePct >= 70f) return "良";
            if (m.abilityScorePct >= 50f) return "可";
            return "弱";
        }

        static string GradeStability(MetricsData m)
        {
            bool ok = m.sustainedDriftPct <= 15f && m.memSlopeMBPerReq <= 2f && m.timeouts == 0 && m.errors == 0
                      && (m.cancelTotal == 0 || m.cancelOkCount == m.cancelTotal);
            return ok ? "稳定" : "有风险";
        }

        public static string BuildText(BenchLog log)
        {
            MetricsData m = log.metrics;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================ 本地 LLM 测试报告 ================");
            sb.AppendLine("状态: " + log.status + "    测试者: " + (string.IsNullOrEmpty(log.tester) ? "-" : log.tester));
            sb.AppendLine("开始: " + log.startedAt + "    结束: " + log.finishedAt + "    总耗时: " + F(log.elapsedSeconds, 0) + " 秒" + (log.isEditor ? "    (在 Unity 编辑器内运行，数据仅供参考)" : ""));
            sb.AppendLine();

            sb.AppendLine("---- 机器 ----");
            sb.AppendLine("系统: " + log.sys.os);
            sb.AppendLine("CPU : " + log.sys.cpu + "  (" + log.sys.cpuThreads + " 线程, " + log.sys.cpuMHz + " MHz)");
            sb.AppendLine("内存: " + log.sys.ramMB + " MB");
            sb.AppendLine("GPU : " + log.sys.gpu + "  (" + log.sys.gpuVendor + ", " + log.sys.vramMB + " MB, " + log.sys.gpuApi + ")");
            sb.AppendLine("设备: " + log.sys.deviceModel + " / " + log.sys.deviceType + " / 电源: " + log.sys.battery);
            sb.AppendLine("Unity " + log.sys.unityVersion + " / " + log.sys.platform + " / " + log.sys.scriptingBackend + " / 分辨率 " + log.sys.screenW + "x" + log.sys.screenH);
            sb.AppendLine("机器标识: " + log.sys.machineId);
            sb.AppendLine();

            sb.AppendLine("---- 模型与参数 ----");
            sb.AppendLine("模型: " + log.config.modelFile + "  (" + log.config.modelSizeMB + " MB)  架构/后端: " + log.config.architecture);
            sb.AppendLine("numGPULayers=" + log.config.numGPULayers + "  numThreads=" + log.config.numThreads + "  contextSize=" + log.config.contextSize +
                          "  batchSize=" + log.config.batchSize + "  flashAttn=" + log.config.flashAttention + "  reasoning=" + log.config.reasoning);
            sb.AppendLine("全局种子=" + log.config.globalSeed + "  单次超时=" + F(log.config.timeoutSeconds, 0) + "s  套件筛选=" + (string.IsNullOrEmpty(log.config.suitesFilter) ? "全部" : log.config.suitesFilter));
            sb.AppendLine("启动参数: " + (string.IsNullOrEmpty(log.config.args) ? "(无)" : log.config.args));
            sb.AppendLine();

            if (log.errors.Count > 0)
            {
                sb.AppendLine("---- 运行中出现的错误 ----");
                for (int i = 0; i < log.errors.Count; i++) sb.AppendLine("  " + log.errors[i]);
                sb.AppendLine();
            }

            sb.AppendLine("---- 加载 ----");
            if (log.load.ok)
            {
                sb.AppendLine("模型加载: " + F(log.load.loadSeconds, 1) + " 秒    首次预热: " + F(log.load.warmupSeconds, 2) + " 秒");
                sb.AppendLine("CPU侧内存采样(不含显存): 加载前 " + F(log.load.memBeforeMB, 0) + " MB → 加载后 " + F(log.load.memAfterMB, 0) + " MB → 峰值 " + F(log.peakMemMB, 0) + " MB");
            }
            else
            {
                sb.AppendLine("加载失败: " + log.load.error);
            }
            sb.AppendLine();

            if (m.requests > 0)
            {
                sb.AppendLine("---- 三个评级 ----");
                sb.AppendLine("速度: " + m.speedGrade + "    能力: " + m.abilityGrade + " (" + F(m.abilityScorePct, 0) + "分)    稳定性: " + m.stabilityGrade);
                sb.AppendLine();

                sb.AppendLine("---- 延迟与速度（共 " + m.requests + " 次请求，超时 " + m.timeouts + "，出错 " + m.errors + "） ----");
                sb.AppendLine("总耗时  平均 " + F(m.totalMean, 2) + "s   P50 " + F(m.totalP50, 2) + "s   P95 " + F(m.totalP95, 2) + "s   最大 " + F(m.totalMax, 2) + "s");
                sb.AppendLine("首字延迟 P50 " + F(m.ttftP50, 2) + "s   P95 " + F(m.ttftP95, 2) + "s");
                sb.AppendLine("生成速度(中位) " + F(m.decodeTpsMedian, 1) + " tok/s    提示词处理(中位) " + F(m.prefillTpsMedian, 1) + " tok/s");
                sb.AppendLine("平均提示词 " + F(m.promptTokMean, 0) + " tokens    平均输出 " + F(m.outTokMean, 0) + " tokens");
                sb.AppendLine();

                sb.AppendLine("---- 游戏/应用流畅度（推理期间主线程） ----");
                sb.AppendLine("空闲帧率 " + F(log.frames.idleFps, 0) + " fps (P99 帧时间 " + F(log.frames.idleP99Ms, 1) + " ms)");
                sb.AppendLine("推理时帧率 " + F(log.frames.genFps, 0) + " fps (P99 " + F(log.frames.genP99Ms, 1) + " ms, 最长 " + F(log.frames.genMaxMs, 0) + " ms)");
                sb.AppendLine("卡顿帧: >50ms " + log.frames.hitches50 + " 次, >100ms " + log.frames.hitches100 + " 次 (共 " + log.frames.genFrames + " 帧)");
                sb.AppendLine();

                if (m.suiteScores.Count > 0)
                {
                    sb.AppendLine("---- 各套件得分 ----");
                    for (int i = 0; i < m.suiteScores.Count; i++)
                    {
                        SuiteScoreRow s = m.suiteScores[i];
                        sb.AppendLine("  " + Pad(s.suite, 4) + " n=" + Pad(s.n.ToString(), 4) + " 得分 " + F(s.scorePct, 1) + "%");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---- 稳定性 / 多样性 ----");
                sb.AppendLine("连续运行耗时漂移: " + F(m.sustainedDriftPct, 1) + "%" + (m.sustainedDriftPct > 15f ? "  ⚠ 可能过热降频" : ""));
                sb.AppendLine("内存斜率: " + F(m.memSlopeMBPerReq, 2) + " MB/请求" + (m.memSlopeMBPerReq > 2f ? "  ⚠ 可能漏内存" : ""));
                if (log.config.suitesFilter.Contains("I"))
                {
                    sb.AppendLine("高温多样性(distinct-2): " + F(m.diversityDistinctPct, 0) + "%");
                    sb.AppendLine("固定种子确定性复现率: " + F(m.determinismPct, 0) + "%");
                }
                sb.AppendLine();

                int shown = 0;
                for (int i = 0; i < log.records.Count && shown < 20; i++)
                {
                    CaseRecord r = log.records[i];
                    if (r.score < 0f || r.score >= 0.999f) continue;
                    if (shown == 0) sb.AppendLine("---- 未通过 / 部分通过的用例（最多列 20 条） ----");
                    shown++;
                    sb.AppendLine("  [" + r.suite + "/" + r.id + "] 得分 " + F(r.score, 2) + "  " + r.note + "   原始输出: “" + Cut(r.raw, 40) + "”");
                }
            }
            return sb.ToString();
        }

        static string Pad(string s, int w) { if (s == null) s = ""; return s.Length >= w ? s : s + new string(' ', w - s.Length); }
        static string Cut(string s, int max) { if (s == null) return ""; s = s.Replace("\n", " "); return s.Length <= max ? s : s.Substring(0, max) + "…"; }

        public static string ShortSummary(BenchLog log)
        {
            MetricsData m = log.metrics;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("状态: " + log.status + "    总耗时 " + F(log.elapsedSeconds, 0) + " 秒");
            if (!log.load.ok) { sb.AppendLine("模型加载失败：" + log.load.error); return sb.ToString(); }
            sb.AppendLine("加载 " + F(log.load.loadSeconds, 1) + "s   预热 " + F(log.load.warmupSeconds, 1) + "s   内存峰值 " + F(log.peakMemMB, 0) + " MB");
            sb.AppendLine("P50 " + F(m.totalP50, 2) + "s / P95 " + F(m.totalP95, 2) + "s   生成 " + F(m.decodeTpsMedian, 1) + " tok/s");
            sb.AppendLine("速度: " + m.speedGrade + "    能力: " + m.abilityGrade + " (" + F(m.abilityScorePct, 0) + "分)    稳定性: " + m.stabilityGrade);
            return sb.ToString();
        }
    }
}

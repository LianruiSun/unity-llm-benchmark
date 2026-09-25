using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Bench
{
    /// <summary>
    /// 把一次测试的完整记录导出成人可读的 CSV 表格，和带图表的 HTML 报告。
    /// 纯字符串拼接，不依赖 UnityEngine、不依赖任何第三方库或 CDN，双击离线打开即可。
    /// 图表颜色取自团队统一的数据可视化色板（categorical slot1=蓝/slot2=橙，独立的 status 色板）。
    /// </summary>
    public static class BenchReport
    {
        // =====================================================================
        // CSV：完整表格，每一条用例的输入/输出/耗时/得分，Excel 可直接打开
        // =====================================================================
        public static string BuildCsv(BenchLog log)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("suite,id,mode,system,user,raw_output,score,note,timeout,error,total_s,ttft_s,prompt_tokens,out_tokens,decode_tps,prefill_tps\n");
            List<CaseRecord> all = log.records;
            for (int i = 0; i < all.Count; i++)
            {
                CaseRecord r = all[i];
                sb.Append(Csv(r.suite)).Append(',');
                sb.Append(Csv(r.id)).Append(',');
                sb.Append(Csv(r.mode)).Append(',');
                sb.Append(Csv(r.system)).Append(',');
                sb.Append(Csv(r.user)).Append(',');
                sb.Append(Csv(r.raw)).Append(',');
                sb.Append(F(r.score, 3)).Append(',');
                sb.Append(Csv(r.note)).Append(',');
                sb.Append(r.timeout ? "1" : "0").Append(',');
                sb.Append(Csv(r.error)).Append(',');
                sb.Append(F(r.total, 3)).Append(',');
                sb.Append(F(r.ttft >= 0f ? r.ttft : 0f, 3)).Append(',');
                sb.Append(r.promptTokens.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(r.outTokens.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(F(r.decodeTps, 2)).Append(',');
                sb.Append(F(r.prefillTps, 2)).Append('\n');
            }
            return sb.ToString();
        }

        static string F(float v, int digits) { return v.ToString("F" + digits, CultureInfo.InvariantCulture); }

        static string Csv(string s)
        {
            if (s == null) s = "";
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");
            bool needsQuote = s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0;
            if (needsQuote) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        static string Cut(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        // =====================================================================
        // HTML 报告：统计卡片 + 图表 + 完整数据表
        // =====================================================================
        public static string BuildHtml(BenchLog log)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"zh\"><head><meta charset=\"utf-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.Append("<title>本地 LLM 测试报告</title><style>").Append(Css()).Append("</style></head>");
            sb.Append("<body class=\"viz-root\"><div class=\"wrap\">");
            sb.Append(BuildHeader(log));
            sb.Append(BuildStatTiles(log));

            bool hasData = log.metrics.requests > 0;
            if (hasData)
            {
                sb.Append("<div class=\"charts\">");
                sb.Append(BuildSuiteScoreChart(log));
                sb.Append(BuildSuiteLatencyChart(log));
                sb.Append(BuildResultBreakdownChart(log));
                sb.Append(BuildTrendChart(log));
                sb.Append("</div>");
                sb.Append(BuildFailures(log));
                sb.Append(BuildTable(log));
            }
            else
            {
                sb.Append("<p class=\"muted\">还没有可展示的请求记录。</p>");
            }
            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        static string BuildHeader(BenchLog log)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<h1>本地 LLM 测试报告</h1>");
            sb.Append("<p class=\"meta\">状态 <b>").Append(Esc(log.status)).Append("</b> · 测试者 ")
              .Append(Esc(string.IsNullOrEmpty(log.tester) ? "-" : log.tester))
              .Append(" · ").Append(Esc(log.startedAt)).Append(" ~ ").Append(Esc(log.finishedAt))
              .Append(" · 总耗时 ").Append(F(log.elapsedSeconds, 0)).Append(" 秒</p>");
            sb.Append("<p class=\"meta\">模型 <b>").Append(Esc(log.config.modelFile)).Append("</b> (")
              .Append(log.config.modelSizeMB).Append(" MB) · GPU层数=").Append(log.config.numGPULayers)
              .Append(" 线程=").Append(log.config.numThreads).Append(" 上下文=").Append(log.config.contextSize)
              .Append(" · 实际后端=").Append(Esc(log.config.architecture))
              .Append(" · 套件=").Append(Esc(log.config.suitesFilter))
              .Append("</p>");
            sb.Append("<p class=\"meta\">机器 ").Append(Esc(log.sys.cpu)).Append(" · ").Append(Esc(log.sys.gpu))
              .Append(" · ").Append(Esc(log.sys.os)).Append("</p>");
            return sb.ToString();
        }

        static string Tile(string label, string value, string sub)
        {
            return "<div class=\"tile\"><div class=\"tile-label\">" + Esc(label) + "</div>" +
                   "<div class=\"tile-value\">" + Esc(value) + "</div>" +
                   (string.IsNullOrEmpty(sub) ? "" : "<div class=\"tile-sub\">" + Esc(sub) + "</div>") + "</div>";
        }

        static string BuildStatTiles(BenchLog log)
        {
            MetricsData m = log.metrics;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"tiles\">");
            sb.Append(Tile("速度评级", m.speedGrade, "生成 " + F(m.decodeTpsMedian, 1) + " tok/s · TTFT P50 " + F(m.ttftP50, 2) + "s"));
            sb.Append(Tile("能力评级", m.abilityGrade, F(m.abilityScorePct, 0) + " 分"));
            sb.Append(Tile("稳定性评级", m.stabilityGrade, "漂移 " + F(m.sustainedDriftPct, 1) + "% · 内存斜率 " + F(m.memSlopeMBPerReq, 2) + " MB/请求"));
            sb.Append(Tile("总请求数", m.requests.ToString(), "超时 " + m.timeouts + " · 出错 " + m.errors));
            sb.Append(Tile("模型加载", F(log.load.loadSeconds, 1) + " s", "预热 " + F(log.load.warmupSeconds, 2) + " s"));
            sb.Append(Tile("CPU侧内存采样峰值", F(log.peakMemMB, 0) + " MB", "不含显存"));
            sb.Append("</div>");
            return sb.ToString();
        }

        // ---------------- 图 1：各套件得分（单系列，蓝色） ----------------
        static string BuildSuiteScoreChart(BenchLog log)
        {
            List<CaeItem> items = new List<CaeItem>();
            List<SuiteScoreRow> rows = log.metrics.suiteScores;
            for (int i = 0; i < rows.Count; i++) items.Add(new CaeItem(rows[i].suite, rows[i].scorePct));
            if (items.Count == 0) return "";
            return ChartCard("各套件得分", "%", BarChartSvg(items, 100f, "分", true));
        }

        // ---------------- 图 2：各套件平均耗时（单系列，橙色） ----------------
        static string BuildSuiteLatencyChart(BenchLog log)
        {
            Dictionary<string, float> sum = new Dictionary<string, float>();
            Dictionary<string, int> cnt = new Dictionary<string, int>();
            List<CaseRecord> all = log.records;
            for (int i = 0; i < all.Count; i++)
            {
                CaseRecord r = all[i];
                if (r.timeout || !string.IsNullOrEmpty(r.error) || r.total <= 0f) continue;
                float sv; int cv;
                sum.TryGetValue(r.suite, out sv); sum[r.suite] = sv + r.total;
                cnt.TryGetValue(r.suite, out cv); cnt[r.suite] = cv + 1;
            }
            List<string> suites = new List<string>(sum.Keys);
            suites.Sort();
            List<CaeItem> items = new List<CaeItem>();
            for (int i = 0; i < suites.Count; i++)
            {
                string su = suites[i];
                items.Add(new CaeItem(su, cnt[su] > 0 ? sum[su] / cnt[su] : 0f));
            }
            if (items.Count == 0) return "";
            return ChartCard("各套件平均耗时", "秒/请求", BarChartSvg(items, -1f, "s", false));
        }

        // ---------------- 图 3：结果分布（状态色：通过/部分/未通过/超时/出错） ----------------
        static string BuildResultBreakdownChart(BenchLog log)
        {
            int pass = 0, partial = 0, fail = 0, timeoutN = 0, errN = 0;
            List<CaseRecord> all = log.records;
            for (int i = 0; i < all.Count; i++)
            {
                CaseRecord r = all[i];
                if (r.timeout) { timeoutN++; continue; }
                if (!string.IsNullOrEmpty(r.error)) { errN++; continue; }
                if (r.score < 0f) continue; // 纯性能项，不参与打分
                if (r.score >= 0.999f) pass++;
                else if (r.score <= 0.001f) fail++;
                else partial++;
            }
            int total = pass + partial + fail + timeoutN + errN;
            if (total == 0) return "";
            List<StatusItem> items = new List<StatusItem>
            {
                new StatusItem("通过", pass, "#0ca30c"),
                new StatusItem("部分", partial, "#fab219"),
                new StatusItem("未通过", fail, "#d03b3b"),
                new StatusItem("超时", timeoutN, "#ec835a"),
                new StatusItem("出错", errN, "#d03b3b"),
            };
            return ChartCard("打分用例结果分布（共 " + total + " 条）", "条", StatusBarSvg(items));
        }

        // ---------------- 图 4：J 套件连续短请求耗时趋势（折线，观察是否漂移） ----------------
        static string BuildTrendChart(BenchLog log)
        {
            List<float> ys = new List<float>();
            List<CaseRecord> all = log.records;
            for (int i = 0; i < all.Count; i++)
            {
                CaseRecord r = all[i];
                if (r.suite == "J" && r.id.StartsWith("J1-", StringComparison.Ordinal) && !r.timeout && string.IsNullOrEmpty(r.error))
                    ys.Add(r.total);
            }
            if (ys.Count < 2) return "";
            return ChartCard("J1 连续短请求耗时趋势（第 1~" + ys.Count + " 次）", "秒", LineChartSvg(ys));
        }

        static string ChartCard(string title, string unit, string svg)
        {
            if (string.IsNullOrEmpty(svg)) return "";
            return "<div class=\"card\"><h2>" + Esc(title) + "</h2>" + svg + "</div>";
        }

        struct CaeItem { public string label; public float value; public CaeItem(string l, float v) { label = l; value = v; } }
        struct StatusItem { public string label; public int value; public string color; public StatusItem(string l, int v, string c) { label = l; value = v; color = c; } }

        const int ChartW = 620, ChartH = 230, PadL = 46, PadR = 16, PadT = 20, PadB = 34;

        static string RoundedTopBarPath(float x, float yTop, float w, float yBase, float r)
        {
            float h = yBase - yTop;
            if (h < 0.5f) return "M" + F(x, 1) + "," + F(yBase, 1) + " L" + F(x + w, 1) + "," + F(yBase, 1);
            float rr = Math.Min(r, Math.Min(w / 2f, h));
            return "M" + F(x, 1) + "," + F(yBase, 1) +
                   " L" + F(x, 1) + "," + F(yTop + rr, 1) +
                   " Q" + F(x, 1) + "," + F(yTop, 1) + " " + F(x + rr, 1) + "," + F(yTop, 1) +
                   " L" + F(x + w - rr, 1) + "," + F(yTop, 1) +
                   " Q" + F(x + w, 1) + "," + F(yTop, 1) + " " + F(x + w, 1) + "," + F(yTop + rr, 1) +
                   " L" + F(x + w, 1) + "," + F(yBase, 1) + " Z";
        }

        /// <summary>单系列竖向柱状图。pctMode=true 时固定 0~100（得分类），否则按数据最大值自适应（耗时类）。</summary>
        static string BarChartSvg(List<CaeItem> items, float fixedMax, string unitSuffix, bool pctMode)
        {
            int n = items.Count;
            float max = fixedMax;
            if (max <= 0f)
            {
                for (int i = 0; i < n; i++) if (items[i].value > max) max = items[i].value;
                max = max <= 0f ? 1f : max * 1.2f;
            }
            float plotW = ChartW - PadL - PadR, plotH = ChartH - PadT - PadB;
            float baseline = PadT + plotH;
            float slot = plotW / n;
            float barW = Math.Min(56f, slot * 0.55f);

            StringBuilder sb = new StringBuilder();
            sb.Append("<svg viewBox=\"0 0 ").Append(ChartW).Append(' ').Append(ChartH).Append("\" class=\"chart\" role=\"img\">");

            // 网格线（4 档）
            for (int g = 0; g <= 4; g++)
            {
                float gv = max * g / 4f;
                float gy = baseline - plotH * g / 4f;
                sb.Append("<line class=\"grid\" x1=\"").Append(F(PadL, 1)).Append("\" x2=\"").Append(F(ChartW - PadR, 1))
                  .Append("\" y1=\"").Append(F(gy, 1)).Append("\" y2=\"").Append(F(gy, 1)).Append("\"/>");
                sb.Append("<text class=\"tick\" x=\"").Append(F(PadL - 8, 1)).Append("\" y=\"").Append(F(gy + 4, 1))
                  .Append("\" text-anchor=\"end\">").Append(pctMode ? F(gv, 0) : F(gv, 1)).Append("</text>");
            }

            for (int i = 0; i < n; i++)
            {
                CaeItem it = items[i];
                float cx = PadL + slot * i + slot / 2f;
                float x = cx - barW / 2f;
                float h = max > 0f ? plotH * Math.Min(it.value, max) / max : 0f;
                float yTop = baseline - h;
                string val = pctMode ? F(it.value, 0) + "%" : F(it.value, 2) + unitSuffix;
                sb.Append("<path class=\"bar\" d=\"").Append(RoundedTopBarPath(x, yTop, barW, baseline, 4f)).Append("\">");
                sb.Append("<title>").Append(Esc(it.label)).Append(": ").Append(Esc(val)).Append("</title></path>");
                sb.Append("<text class=\"barval\" x=\"").Append(F(cx, 1)).Append("\" y=\"").Append(F(yTop - 6, 1))
                  .Append("\" text-anchor=\"middle\">").Append(Esc(val)).Append("</text>");
                sb.Append("<text class=\"axislabel\" x=\"").Append(F(cx, 1)).Append("\" y=\"").Append(F(baseline + 20, 1))
                  .Append("\" text-anchor=\"middle\">").Append(Esc(it.label)).Append("</text>");
            }
            sb.Append("<line class=\"axis\" x1=\"").Append(F(PadL, 1)).Append("\" x2=\"").Append(F(ChartW - PadR, 1))
              .Append("\" y1=\"").Append(F(baseline, 1)).Append("\" y2=\"").Append(F(baseline, 1)).Append("\"/>");
            sb.Append("</svg>");
            return sb.ToString();
        }

        /// <summary>状态色横向堆叠条（结果分布）。</summary>
        static string StatusBarSvg(List<StatusItem> items)
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++) total += items[i].value;
            if (total <= 0) return "";
            float w = ChartW - PadL - PadR;
            float barY = 30, barH = 34;
            StringBuilder sb = new StringBuilder();
            int svgH = 60 + items.Count * 26;
            sb.Append("<svg viewBox=\"0 0 ").Append(ChartW).Append(' ').Append(svgH).Append("\" class=\"chart\" role=\"img\">");
            float x = PadL;
            for (int i = 0; i < items.Count; i++)
            {
                StatusItem it = items[i];
                if (it.value <= 0) continue;
                float segW = w * it.value / total;
                sb.Append("<rect x=\"").Append(F(x, 1)).Append("\" y=\"").Append(F(barY, 1)).Append("\" width=\"")
                  .Append(F(Math.Max(0f, segW - 2f), 1)).Append("\" height=\"").Append(F(barH, 1))
                  .Append("\" rx=\"3\" fill=\"").Append(it.color).Append("\">");
                sb.Append("<title>").Append(Esc(it.label)).Append(": ").Append(it.value).Append(" 条</title></rect>");
                x += segW;
            }
            // 图例
            float ly = barY + barH + 22;
            float lx = PadL;
            for (int i = 0; i < items.Count; i++)
            {
                StatusItem it = items[i];
                sb.Append("<rect x=\"").Append(F(lx, 1)).Append("\" y=\"").Append(F(ly - 10, 1)).Append("\" width=\"10\" height=\"10\" rx=\"2\" fill=\"").Append(it.color).Append("\"/>");
                sb.Append("<text class=\"axislabel\" x=\"").Append(F(lx + 15, 1)).Append("\" y=\"").Append(F(ly - 1, 1)).Append("\">")
                  .Append(Esc(it.label)).Append(' ').Append(it.value).Append("</text>");
                ly += 24;
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        /// <summary>单系列折线图，带每点 hover 提示。</summary>
        static string LineChartSvg(List<float> ys)
        {
            int n = ys.Count;
            float max = 0f, min = float.MaxValue;
            for (int i = 0; i < n; i++) { if (ys[i] > max) max = ys[i]; if (ys[i] < min) min = ys[i]; }
            if (min > max) min = 0f;
            float pad = (max - min) * 0.15f; if (pad < 0.05f) pad = 0.05f;
            float loY = Math.Max(0f, min - pad), hiY = max + pad;
            float plotW = ChartW - PadL - PadR, plotH = ChartH - PadT - PadB;
            float baseline = PadT + plotH;

            Func<int, float> px = i => PadL + (n <= 1 ? 0f : plotW * i / (n - 1));
            Func<float, float> py = v => baseline - plotH * (v - loY) / Math.Max(0.0001f, hiY - loY);

            StringBuilder sb = new StringBuilder();
            sb.Append("<svg viewBox=\"0 0 ").Append(ChartW).Append(' ').Append(ChartH).Append("\" class=\"chart\" role=\"img\">");
            for (int g = 0; g <= 4; g++)
            {
                float gv = loY + (hiY - loY) * g / 4f;
                float gy = baseline - plotH * g / 4f;
                sb.Append("<line class=\"grid\" x1=\"").Append(F(PadL, 1)).Append("\" x2=\"").Append(F(ChartW - PadR, 1))
                  .Append("\" y1=\"").Append(F(gy, 1)).Append("\" y2=\"").Append(F(gy, 1)).Append("\"/>");
                sb.Append("<text class=\"tick\" x=\"").Append(F(PadL - 8, 1)).Append("\" y=\"").Append(F(gy + 4, 1))
                  .Append("\" text-anchor=\"end\">").Append(F(gv, 2)).Append("</text>");
            }
            StringBuilder pts = new StringBuilder();
            for (int i = 0; i < n; i++) pts.Append(F(px(i), 1)).Append(',').Append(F(py(ys[i]), 1)).Append(' ');
            sb.Append("<polyline class=\"line\" points=\"").Append(pts.ToString()).Append("\"/>");
            for (int i = 0; i < n; i++)
            {
                sb.Append("<circle class=\"dot\" cx=\"").Append(F(px(i), 1)).Append("\" cy=\"").Append(F(py(ys[i]), 1)).Append("\" r=\"3\">");
                sb.Append("<title>第 ").Append(i + 1).Append(" 次: ").Append(F(ys[i], 2)).Append("s</title></circle>");
            }
            sb.Append("<line class=\"axis\" x1=\"").Append(F(PadL, 1)).Append("\" x2=\"").Append(F(ChartW - PadR, 1))
              .Append("\" y1=\"").Append(F(baseline, 1)).Append("\" y2=\"").Append(F(baseline, 1)).Append("\"/>");
            sb.Append("</svg>");
            return sb.ToString();
        }

        // ---------------- 完整数据表：所有测试的输入/输出/耗时/得分 ----------------
        static string BuildFailures(BenchLog log)
        {
            StringBuilder sb = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < log.records.Count && shown < 20; i++)
            {
                CaseRecord r = log.records[i];
                if (!r.timeout && string.IsNullOrEmpty(r.error) && (r.score < 0f || r.score >= 0.999f)) continue;
                if (shown++ == 0) sb.Append("<section class=\"failures\"><h2>需要查看的用例</h2><ul>");
                sb.Append("<li><b>").Append(Esc(r.suite)).Append('/').Append(Esc(r.id)).Append("</b> · ")
                  .Append(Esc(r.timeout ? "超时" : !string.IsNullOrEmpty(r.error) ? r.error : r.note))
                  .Append("<br>输出：<code>").Append(Esc(Cut(r.raw, 160))).Append("</code></li>");
            }
            if (shown > 0) sb.Append("</ul></section>");
            return sb.ToString();
        }

        static string BuildTable(BenchLog log)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>完整用例表（共 ").Append(log.records.Count).Append(" 条）</h2>");
            sb.Append("<div class=\"tablewrap\"><table><thead><tr>");
            sb.Append("<th>套件/ID</th><th>模式</th><th>System</th><th>输入</th><th>输出</th><th>得分</th><th>备注</th>");
            sb.Append("<th>耗时(s)</th><th>首字(s)</th><th>提示词tok</th><th>输出tok</th><th>生成tok/s</th>");
            sb.Append("</tr></thead><tbody>");
            List<CaseRecord> all = log.records;
            for (int i = 0; i < all.Count; i++)
            {
                CaseRecord r = all[i];
                string scoreCls = r.timeout || !string.IsNullOrEmpty(r.error) ? "s-bad"
                    : r.score < 0f ? "s-na" : r.score >= 0.999f ? "s-good" : r.score <= 0.001f ? "s-bad" : "s-mid";
                string scoreTxt = r.timeout ? "超时" : !string.IsNullOrEmpty(r.error) ? "出错" : r.score < 0f ? "-" : F(r.score * 100f, 0) + "%";
                sb.Append("<tr>");
                sb.Append("<td class=\"mono\">").Append(Esc(r.suite)).Append('/').Append(Esc(r.id)).Append("</td>");
                sb.Append("<td>").Append(Esc(r.mode)).Append("</td>");
                sb.Append("<td class=\"cell\" title=\"").Append(Esc(r.system)).Append("\">").Append(Esc(Cut(r.system, 20))).Append("</td>");
                sb.Append("<td class=\"cell mono\">").Append(Esc(r.user)).Append("</td>");
                sb.Append("<td class=\"cell mono\">").Append(Esc(r.raw)).Append("</td>");
                sb.Append("<td class=\"").Append(scoreCls).Append("\">").Append(scoreTxt).Append("</td>");
                sb.Append("<td class=\"cell\">").Append(Esc(Cut(r.note + r.error, 60))).Append("</td>");
                sb.Append("<td class=\"num\">").Append(F(r.total, 2)).Append("</td>");
                sb.Append("<td class=\"num\">").Append(r.ttft >= 0f ? F(r.ttft, 2) : "-").Append("</td>");
                sb.Append("<td class=\"num\">").Append(r.promptTokens).Append("</td>");
                sb.Append("<td class=\"num\">").Append(r.outTokens).Append("</td>");
                sb.Append("<td class=\"num\">").Append(r.decodeTps > 0f ? F(r.decodeTps, 1) : "-").Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        static string Css()
        {
            return @"
:root{color-scheme:light;--surface:#fcfcfb;--page:#f9f9f7;--ink:#0b0b0b;--ink2:#52514e;--muted:#898781;
--grid:#e1e0d9;--axis:#c3c2b7;--border:rgba(11,11,11,0.10);--blue:#2a78d6;--orange:#eb6834;}
@media (prefers-color-scheme: dark){:root{color-scheme:dark;--surface:#1a1a19;--page:#0d0d0d;--ink:#ffffff;--ink2:#c3c2b7;
--muted:#898781;--grid:#2c2c2a;--axis:#383835;--border:rgba(255,255,255,0.10);--blue:#3987e5;--orange:#d95926;}}
*{box-sizing:border-box}
body{margin:0;background:var(--page);color:var(--ink);font-family:system-ui,-apple-system,""Segoe UI"",""Microsoft YaHei UI"",sans-serif;}
.wrap{max-width:1100px;margin:0 auto;padding:28px 20px 60px}
h1{font-size:22px;margin:0 0 8px}
h2{font-size:16px;margin:0 0 12px;color:var(--ink)}
p.meta{margin:2px 0;color:var(--ink2);font-size:13px}
.muted{color:var(--muted)}
.failures{background:var(--surface);border:1px solid var(--border);border-radius:8px;padding:14px;margin:20px 0}
.failures ul{margin:0;padding-left:20px}.failures li{margin:8px 0;line-height:1.5}.failures code{white-space:pre-wrap;overflow-wrap:anywhere}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px;margin:20px 0}
.tile{background:var(--surface);border:1px solid var(--border);border-radius:8px;padding:12px 14px}
.tile-label{font-size:12px;color:var(--muted)}
.tile-value{font-size:22px;font-weight:600;margin-top:2px}
.tile-sub{font-size:12px;color:var(--ink2);margin-top:2px}
.charts{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:16px;margin:20px 0}
.card{background:var(--surface);border:1px solid var(--border);border-radius:8px;padding:14px}
.chart{width:100%;height:auto;display:block}
.grid{stroke:var(--grid);stroke-width:1}
.axis{stroke:var(--axis);stroke-width:1}
.tick,.axislabel{fill:var(--muted);font-size:10px}
.barval{fill:var(--ink2);font-size:11px}
.bar{fill:var(--blue)}
.line{fill:none;stroke:var(--blue);stroke-width:2}
.dot{fill:var(--blue)}
.tablewrap{overflow-x:auto;border:1px solid var(--border);border-radius:8px}
table{border-collapse:collapse;width:100%;font-size:12px;min-width:900px}
th,td{padding:6px 8px;border-bottom:1px solid var(--border);text-align:left;vertical-align:top}
th{position:sticky;top:0;background:var(--surface);color:var(--ink2);font-weight:600}
td.cell{max-width:260px;white-space:pre-wrap;word-break:break-word}
td.mono,th.mono{font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace}
td.num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
td.s-good{color:#0ca30c;font-weight:600}
td.s-mid{color:#fab219;font-weight:600}
td.s-bad{color:#d03b3b;font-weight:600}
td.s-na{color:var(--muted)}
tr:hover td{background:var(--page)}
";
        }
    }
}

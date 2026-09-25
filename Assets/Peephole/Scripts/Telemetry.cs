namespace Peephole
{
    /// <summary>
    /// 输入遥测：玩家怎么打字，比打了什么字更能暴露状态。
    /// 代码只统计几个数字，转成"他删掉了一句话"这样的句子，再让模型去"读"。
    /// </summary>
    public class Telemetry
    {
        float promptTime;
        float firstKeyTime;
        int deletions;
        int prevLen;
        bool typed;

        public void BeginTurn(float now)
        {
            promptTime = now;
            firstKeyTime = -1f;
            deletions = 0;
            prevLen = 0;
            typed = false;
        }

        public void OnValueChanged(string text, float now)
        {
            int len = text == null ? 0 : text.Length;
            if (!typed && len > 0)
            {
                typed = true;
                firstKeyTime = now;
            }
            if (len < prevLen) deletions++;
            prevLen = len;
        }

        /// <summary>提交时调用：把行为翻译成事件句子，并对隐藏变量做确定性的小修正。</summary>
        public void Finish(GameState s, string text, float now)
        {
            if (firstKeyTime >= 0f && firstKeyTime - promptTime > 8f)
            {
                s.events.Add("他站了很久，才开口");
                s.suspicion += 3f;
            }

            if (deletions >= 8)
            {
                s.events.Add("他反复删改了很多次，像是在斟酌怎么说");
                s.suspicion += 4f;
            }
            else if (deletions >= 4)
            {
                s.events.Add("他删改了几次才说出口");
                s.suspicion += 2f;
            }

            int len = text == null ? 0 : text.Length;
            if (firstKeyTime >= 0f && now - firstKeyTime < 1.2f && len >= 10)
            {
                s.events.Add("他几乎没有停顿，话说得很流利，像是早就准备好的");
                s.suspicion += 2f;
            }

            s.Clamp();
        }
    }
}

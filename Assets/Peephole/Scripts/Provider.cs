using System.Threading;
using System.Threading.Tasks;

namespace Peephole
{
    /// <summary>一回合要问模型的"情境"。全部由代码构造，玩家原话只作为被引用的数据出现。</summary>
    public class Situation
    {
        public string systemPrompt;
        public string userMessage;
        public string playerText;
        public GameState state;
    }

    /// <summary>
    /// 决策提供者的统一接口。LlmDecisionProvider（本地模型）、MockDecisionProvider（规则）
    /// 都实现它，所以整个游戏可以在没有模型的情况下开发、测试和回放。
    /// </summary>
    public interface IDecisionProvider
    {
        string Name { get; }
        Task<Decision> DecideAsync(Situation s, CancellationToken ct);
    }
}

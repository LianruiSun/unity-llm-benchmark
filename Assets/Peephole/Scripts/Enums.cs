namespace Peephole
{
    /// <summary>门后的人下一步可以做的动作。模型只能从这个封闭集合里选。</summary>
    public enum Intent { Silent, Ask, Refuse, Warn, Crack, Open, Slam, Police, Withdraw }

    /// <summary>模型对门外玩家语气的判断（封闭枚举）。</summary>
    public enum Tone { Calm, Friendly, Pleading, Angry, Deceptive, Nonsense }

    /// <summary>模型对门外玩家威胁程度的判断（封闭枚举）。</summary>
    public enum Threat { None, Low, High }

    public enum DoorState { Closed, Chain, Open }

    /// <summary>门后的人的性格。每局随机，玩家看不到，只能从行为里推断。</summary>
    public enum Temperament { Lonely, Paranoid, Predator }

    public enum EndingId { None, WelcomeLonely, WelcomeParanoid, WelcomePredator, DrivenAway, Police, Left }

    public enum Phase { Intro, Loading, Composing, Thinking, Performing, Ended }
}

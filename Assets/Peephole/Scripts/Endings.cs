namespace Peephole
{
    public class EndingContent
    {
        public string title;
        public string body;
        public string reveal;
    }

    /// <summary>结局由隐藏变量和门后人的性格共同决定，全部是固定文本。</summary>
    public static class Endings
    {
        static string WhoWasBehind(Temperament t)
        {
            switch (t)
            {
                case Temperament.Lonely: return "门后的人：一个太久没有人和他说话的人";
                case Temperament.Paranoid: return "门后的人：一个从不相信任何人的人";
                default: return "门后的人：一个早就在等你的人";
            }
        }

        public static EndingContent Get(EndingId id, Temperament t)
        {
            EndingContent c = new EndingContent();
            c.reveal = WhoWasBehind(t) + "\n下一次，门后可能是另一个人。";
            switch (id)
            {
                case EndingId.WelcomeLonely:
                    c.title = "被留下";
                    c.body = "他把每一盏灯都打开了。他问你要不要喝茶，说了很多话，一直说到天亮。\n直到你想走的时候，才发现这扇门从里面看，没有把手。";
                    break;
                case EndingId.WelcomeParanoid:
                    c.title = "被检查";
                    c.body = "他让你站在玄关，把口袋一个一个翻出来。他检查了你带来的每一样东西，然后又检查了一遍。\n“你先别动，”他说，“我还没想好。”";
                    break;
                case EndingId.WelcomePredator:
                    c.title = "门在身后合上";
                    c.body = "锁舌转了三圈。\n“终于。”他说。\n你忽然意识到，从一开始，他就没有问过你是谁。";
                    break;
                case EndingId.DrivenAway:
                    c.title = "被赶走";
                    c.body = "猫眼暗了下去，再也没有亮起来。\n走廊的灯在你身后，一盏一盏地灭掉。\n你不知道自己错过了什么。";
                    break;
                case EndingId.Police:
                    c.title = "被报警";
                    c.body = "楼下传来警笛声。\n他没有再从猫眼里看你。\n也许，他从一开始就没有打算开门。";
                    break;
                case EndingId.Left:
                    c.title = "你走了";
                    c.body = "你转身离开。走到楼梯口时，你回头看了一眼：\n猫眼里有一点光，一直跟着你，直到看不见。";
                    break;
                default:
                    c.title = "……";
                    c.body = "";
                    break;
            }
            return c;
        }
    }
}

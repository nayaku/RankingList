using System.CommandLine;

namespace RankingListTestNew
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Command("generate", "初始化测试环境，生成用户数据和操作列表")
                {
                    new Argument<string>("name")
                    {
                            Description = "排行榜名称"
                    }
                },
                new Command("base", "生成基准结果数据"),
                new Command("test", "测试指定名称的排行榜，并与基准对比")
                {
                    new Argument<string>("name")
                    {
                        Description = "排行榜名称"
                    }
                }
            };

        }

        static void ShowHelp()
        {
            Console.WriteLine("使用方法:");
            Console.WriteLine("  RankingListTest --generate <name>                  初始化测试环境，生成用户数据和操作列表");
            Console.WriteLine("  RankingListTest --base                生成基准结果数据");
            Console.WriteLine("  RankingListTest --test <name>         测试指定名称的排行榜，并与基准对比");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  RankingListTest --init                初始化测试环境");
            Console.WriteLine("  RankingListTest --base                生成基准数据");
            Console.WriteLine("  RankingListTest --test SimpleRankingList 测试SimpleRankingList");
        }
    }
}

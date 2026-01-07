using System;

namespace RankingListTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 排行榜测试框架 ===");
            Console.WriteLine();

            try
            {
                if (args.Length == 0)
                {
                    ShowHelp();
                    return;
                }

                var testCore = new RankingListTestCore();

                // 解析命令行参数
            if (args[0] == "--init")
            {
                // 初始化模式：生成操作列表和用户数据
                testCore.GenerateInitialUsers();
                testCore.GenerateOperations();
            }
            else if (args[0] == "--base")
            {
                // 基准模式：生成基准结果
                Console.WriteLine("=== 生成基准数据 ===");
                Console.WriteLine("运行基准测试（SimpleRankingList）...");
                var baseResult = testCore.RunTest("SimpleRankingList", true);
                testCore.SaveBaseResult(baseResult);
                Console.WriteLine("\n=== 基准测试结果 ===");
                testCore.DisplayTestResult(baseResult);
            }
            else if (args[0] == "--test" && args.Length > 1)
            {
                // 测试模式：测试指定名称的排行榜
                string rankingListName = args[1];
                Console.WriteLine($"=== 测试 {rankingListName} 排行榜 ===");
                
                var testResult = testCore.RunTest(rankingListName);
                
                Console.WriteLine("\n=== 测试结果 ===");
                testCore.DisplayTestResult(testResult);
                
                // 与基准结果对比
                testCore.CompareWithBase(testResult);
            }
            else
            {
                ShowHelp();
            }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static void ShowHelp()
        {
            Console.WriteLine("使用方法:");
            Console.WriteLine("  RankingListTest --init                初始化测试环境，生成用户数据和操作列表");
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
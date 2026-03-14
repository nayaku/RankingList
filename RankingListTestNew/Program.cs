using System.CommandLine;

namespace RankingListTestNew
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Command generatorCommand = new("generate", "生成测试数据：生成用户数据和操作列表")
            {
                new Argument<string>("name")
                {
                    Description = "测试名"
                },
                new Argument<int>("userNum")
                {
                    Description = "用户数量"
                },
                new Argument<int>("operationNum")
                {
                    Description = "操作数量"
                },
                new Option<TestOperationType?>(
                    "--limitOperationType","-l"
                )
                {
                    Description = "限制操作类型，默认为不限"
                }
            };
            generatorCommand.SetAction(parseResult =>
            {
                string name = parseResult.GetValue<string>("name")!;
                int userNum = parseResult.GetValue<int>("userNum");
                int operationNum = parseResult.GetValue<int>("operationNum");
                TestOperationType? limitOperationType = parseResult.GetValue<TestOperationType?>("--limitOperationType");
                if (Path.Exists(Path.Combine("Test", name + ".json")))
                {
                    Console.WriteLine($"测试数据{name}已存在，覆盖还是重命名？(o/r)");
                    string input = Console.ReadLine();
                    if (input == "o")
                    {
                        Console.WriteLine($"覆盖测试数据{name}");
                    }
                    else if (input == "r")
                    {
                        int suffix = 1;
                        while (Path.Exists(Path.Combine("Test", $"{name}_{suffix}.json")))
                        {
                            suffix++;
                        }
                        name = $"{name}_{suffix}";
                        Console.WriteLine($"使用测试名{name}");
                    }
                    else
                    {
                        Console.WriteLine("输入无效，取消生成");
                        return;
                    }
                }
                Generator generator = new(name, userNum, operationNum, limitOperationType);
                generator.Generate();
            });


            Command testCommand = new("test", "执行测试")
            {
                new Argument<string>("rankingListClassName")
                {
                    Description = "排行榜类名"
                },
                new Option<string?>(
                    "--base","-b"
                )
                {
                    Description = "基准测试名，默认为null"
                },
                new Option<string>(
                    "--testName","-t"
                )
                {
                    Description = "测试名"
                }
            };
            testCommand.SetAction(parseResult =>
            {
                string rankingListClassName = parseResult.GetValue<string>("rankingListClassName")!;
                string? baseTestName = parseResult.GetValue<string?>("--base");
                string? testName = parseResult.GetValue<string>("--testName");
                if (testName != null)
                    TestOperator.Test(rankingListClassName, testName, baseTestName);
                else
                    TestOperator.TestAll(rankingListClassName, baseTestName);
            });

            Command displayCommand = new("display", "显示测试结果")
            {
                new Argument<string>("rankingListClassName")
                {
                    Description = "排行榜类名"
                },
                new Option<string?>(
                    "--base","-b"
                )
                {
                    Description = "基准测试名，默认为null"
                }
            };
            displayCommand.SetAction(parseResult =>
            {
                string rankingListClassName = parseResult.GetValue<string>("rankingListClassName")!;
                string? baseTestName = parseResult.GetValue<string?>("--base");
                TestOperator.TestAll(rankingListClassName, baseTestName);
            });

            RootCommand rootCommand = new()
            {
                generatorCommand,
                testCommand
            };
            ParseResult parseResult = rootCommand.Parse(args);
            return parseResult.Invoke();
        }
    }
}

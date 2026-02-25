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
                Generator generator = new(name, userNum, operationNum, limitOperationType);
                generator.Generate();
            });

            Command fastGeneratorCommand = new("fastGenerate", "快速生成测试数据：生成用户数据和操作列表")
            {
            };
            fastGeneratorCommand.SetAction(p =>
            {
                FastGenerator fastGenerator = new();
                fastGenerator.Generate();
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
                }
            };
            testCommand.SetAction(parseResult =>
            {
                string rankingListClassName = parseResult.GetValue<string>("rankingListClassName")!;
                string? baseTestName = parseResult.GetValue<string?>("--base");
                TestOperator.TestAll(rankingListClassName, baseTestName);
            });

            RootCommand rootCommand = new()
            {
                generatorCommand,
                fastGeneratorCommand,
                testCommand
            };
            ParseResult parseResult = rootCommand.Parse(args);
            return parseResult.Invoke();
        }
    }
}

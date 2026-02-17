using RankingListNew;
using System.CommandLine;
using System.Text.Json;

namespace RankingListTestNew
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var generatorCommand = new Command("generate", "生成测试数据：生成用户数据和操作列表")
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
                new Option<OperationType?>(
                    "--limitOperationType","-l"
                )
                {
                    Description = "限制操作类型，默认为不限"
                }
            };
            generatorCommand.SetAction(parseResult =>
            {
                var name = parseResult.GetValue<string>("name")!;
                var userNum = parseResult.GetValue<int>("userNum");
                var operationNum = parseResult.GetValue<int>("operationNum");
                var limitOperationType = parseResult.GetValue<OperationType?>("--limitOperationType");
                var generator = new Generator(name, userNum, operationNum, limitOperationType);
                generator.Generate();
            });

            var fastGeneratorCommand = new Command("fastGenerate", "快速生成测试数据：生成用户数据和操作列表")
            {
            };
            fastGeneratorCommand.SetAction(p =>
            {
                var fastGenerator = new FastGenerator();
                fastGenerator.Generate();
            });
            var testCommand = new Command("test", "执行测试")
            {
                new Argument<string>("rankingListClassName")
                {
                    Description = "排行榜类名"
                },
                new Option<string?>(
                    "--baseTestName","-b"
                )
                {
                    Description = "基准测试名，默认为null"
                }
            };
            testCommand.SetAction(parseResult =>
            {
                var rankingListClassName = parseResult.GetValue<string>("rankingListClassName")!;
                var baseTestName = parseResult.GetValue<string?>("--baseTestName");
                TestOperator.TestAll(rankingListClassName, baseTestName);
            });

            var rootCommand = new RootCommand
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

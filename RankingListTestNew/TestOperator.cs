using RankingListNew;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using Console = Colorful.Console;

namespace RankingListTestNew
{
    public class TestOperator
    {
        private static readonly DateTime InitialUserCreateTime = new(2300, 1, 1);
        private long _peakMemoryUsage;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly string _testName;
        private readonly string _rankingListClassName;
        private readonly string? _baseRankingListClassName;

        public TestOperator(string rankingListClassName, string testName, string? baseTestName)
        {
            _rankingListClassName = rankingListClassName;
            _testName = testName;
            _baseRankingListClassName = baseTestName;
        }

        public void Test()
        {
            Console.WriteLine($"== Test {_testName} ===");
            TestData testData = LoadTestData();
            Console.WriteLine($"用户数: {testData.Users.Count}");
            Console.WriteLine($"操作数: {testData.Operations.Count}");
            if (testData.LimitOperationType != null)
            {
                Console.WriteLine($"限制操作类型: {testData.LimitOperationType.Value}");
            }
            IRankingList rankingList = RankingListHelper.NewRankingList(_rankingListClassName, testData.Users);
            GC.Collect();
            // 开始内存监控
            long initialMemoryUsage = GC.GetTotalMemory(true);
            _peakMemoryUsage = initialMemoryUsage;
            _cancellationTokenSource = new CancellationTokenSource();
            Thread memoryMonitorThread = new(MonitorMemoryUsage) { IsBackground = true };
            memoryMonitorThread.Start();
            // 运行测试
            (List<OperationResult> operationResults, Stopwatch stopwatch) = RunTest(rankingList, testData);
            // 停止内存监控
            Thread.Sleep(100); // 等待内存监控线程更新峰值
            _cancellationTokenSource.Cancel();
            memoryMonitorThread.Join();

            // 计算测试结果
            TestResult testResultObj = new()
            {
                RankingListName = _rankingListClassName,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                AverageTimeMs = stopwatch.ElapsedMilliseconds / (double)testData.Operations.Count,
                MemoryUsageBytes = GC.GetTotalMemory(true) - initialMemoryUsage,
                PeakMemoryUsageBytes = _peakMemoryUsage - initialMemoryUsage,
                TestDate = DateTime.Now,
            };
            Save(testResultObj, operationResults);
            Console.WriteLine($"排行榜用户数: {rankingList.GetRankingCount()}");
            DisplayTestResult(testResultObj);
            if (_baseRankingListClassName != null)
            {
                ValidateResults(operationResults);
                CompareWithBase(testResultObj);
            }
#if DEBUG
            rankingList.DebugPrint();
#endif
            Console.WriteLine($"== Test {_testName} End ===\n");
        }

        private TestData LoadTestData()
        {
            string testTargetDir = "Test";
            string testDataPath = $"{testTargetDir}/{_testName}.json";
            using FileStream fs = new(testDataPath, FileMode.Open, FileAccess.Read);
            TestData testData = JsonSerializer.Deserialize<TestData>(fs, new JsonSerializerOptions { IncludeFields = true }) ??
                                throw new Exception($"无法加载测试数据 {testDataPath}");
            return testData;
        }

        private (List<OperationResult>, Stopwatch) RunTest(IRankingList rankingList, TestData testData)
        {
            List<OperationResult> operationResults = new(testData.Operations.Count + 1);
            Stopwatch stopwatch = new();
            stopwatch.Start();
            foreach (TestOperation testOperation in testData.Operations)
            {
                OperationResult operationResult = new()
                {
                    Id = testOperation.Id,
                    OperationType = testOperation.Type,
                };
                switch (testOperation.Type)
                {
                    case TestOperationType.AddUser:
                        {
                            User user = new(testOperation.UserId, testOperation.ScoreOrN,
                            InitialUserCreateTime.AddSeconds(testOperation.Id));
                            operationResult.Rank = rankingList.AddUser(user);
                            break;
                        }
                    case TestOperationType.UpdateUser:
                        {
                            User user = new(testOperation.UserId, testOperation.ScoreOrN,
                            InitialUserCreateTime.AddSeconds(testOperation.Id));
                            operationResult.Rank = rankingList.UpdateUser(user);
                            break;
                        }

                    case TestOperationType.GetUserRank:
                        operationResult.Rank = rankingList.GetUserRank(testOperation.UserId);
                        break;
                    case TestOperationType.GetTopN:
                        operationResult.Users = rankingList.GetTopN(testOperation.ScoreOrN);
                        break;
                    case TestOperationType.GetAroundUser:
                        (operationResult.Users, operationResult.Rank) =
                            rankingList.GetAroundUser(testOperation.UserId, testOperation.ScoreOrN);
                        break;
                }

                operationResults.Add(operationResult);
            }

            stopwatch.Stop();
            return (operationResults, stopwatch);
        }

        /// <summary>
        /// 监控内存使用情况
        /// </summary>
        private void MonitorMemoryUsage()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                long currentMemory = GC.GetTotalMemory(false);
                if (currentMemory > _peakMemoryUsage)
                {
                    _peakMemoryUsage = currentMemory;
                }

                Thread.Sleep(10);
            }
        }

        private void Save(TestResult testResultObj, List<OperationResult> operationResults)
        {
            string testResultDir = $"TestResults/{_rankingListClassName}";
            if (!Directory.Exists(testResultDir))
            {
                Directory.CreateDirectory(testResultDir);
            }

            string testResultPath = $"{testResultDir}/{_testName}.json";
            using (FileStream fs = new(testResultPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(fs, testResultObj, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
            }

            string operationResultPath = $"{testResultDir}/{_testName}_Operations.json";
            using (FileStream fs = new(operationResultPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(fs, operationResults, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
            }
        }

        /// <summary>
        /// 验证测试结果与基准
        /// </summary>
        /// <param name="testResults"></param>
        private void ValidateResults(List<OperationResult> testResults)
        {
            string baseTestResultDirPath = $"TestResults/{_baseRankingListClassName}";
            string baseTestResultPath = $"{baseTestResultDirPath}/{_testName}_Operations.json";
            List<OperationResult> baseResults;
            using (FileStream fs = new(baseTestResultPath, FileMode.Open, FileAccess.Read))
            {
                baseResults = JsonSerializer.Deserialize<List<OperationResult>>(fs, new JsonSerializerOptions { IncludeFields = true }) ??
                              throw new Exception($"无法加载基准测试数据 {baseTestResultPath}");
            }

            if (baseResults.Count != testResults.Count)
            {
                throw new Exception("基准测试数据与测试数据操作数不一致");
            }

            int errorCount = 0;
            for (int i = 0; i < baseResults.Count; i++)
            {
                OperationResult baseResult = baseResults[i];
                OperationResult testResult = testResults[i];
                if (baseResult.OperationType != testResult.OperationType)
                {
                    Console.WriteLine($"基准测试数据与测试数据操作类型不一致，第{i}个操作");
                    errorCount++;
                }

                if (baseResult.Rank != testResult.Rank)
                {
                    Console.WriteLine($"基准测试数据与测试数据排名不一致，第{i}个操作");
                    errorCount++;
                }

                if (baseResult.Users is null && testResult.Users is null)
                {
                    continue;
                }

                if (baseResult.Users is null || testResult.Users is null)
                {
                    Console.WriteLine($"基准测试数据与测试数据响应不一致，第{i}个操作");
                    errorCount++;
                    continue;
                }

                if (baseResult.Users.Length != testResult.Users.Length)
                {
                    Console.WriteLine($"基准测试数据与测试数据响应不一致，第{i}个操作");
                    errorCount++;
                    continue;
                }

                for (int j = 0; j < baseResult.Users.Length; j++)
                {
                    if (baseResult.Users[j].CompareTo(testResult.Users[j]) != 0)
                    {
                        Console.WriteLine($"基准测试数据与测试数据响应不一致，第{i}个操作，第{j}个响应");
                        errorCount++;
                    }
                }
            }
            if (errorCount > 0)
            {
                Console.WriteLine($"测试数据与基准数据不一致，共{errorCount}个错误", Color.Red);
            }
            else
            {
                Console.WriteLine("√ 所有操作结果验证通过！", Color.Green);
            }
        }

        /// <summary>
        /// 对比测试结果与基准
        /// </summary>
        /// <param name="testResult"></param>
        private void CompareWithBase(TestResult testResult)
        {
            string baseTestResultDirPath = $"TestResults/{_baseRankingListClassName}";
            string baseTestResultPath = $"{baseTestResultDirPath}/{_testName}.json";
            TestResult baseTestResult;
            using (FileStream fs = new(baseTestResultPath, FileMode.Open, FileAccess.Read))
            {
                baseTestResult = JsonSerializer.Deserialize<TestResult>(fs, new JsonSerializerOptions { IncludeFields = true }) ??
                                 throw new Exception($"无法加载基准测试数据 {baseTestResultPath}");
            }

            Console.WriteLine(
                $"总耗时: {testResult.TotalTimeMs} ms vs {baseTestResult.TotalTimeMs} ms " +
                $"({CalculateDifference(testResult.TotalTimeMs, baseTestResult.TotalTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"平均耗时: {1000 * testResult.AverageTimeMs:0.00} ms/1k操作 vs {1000 * baseTestResult.AverageTimeMs:0.00} ms/1k操作 " +
                $"({CalculateDifference(1000 * testResult.AverageTimeMs, 1000 * baseTestResult.AverageTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"内存占用: {BytesToMB(testResult.MemoryUsageBytes):0.00} MB vs {BytesToMB(baseTestResult.MemoryUsageBytes):0.00} MB " +
                $"({CalculateDifference(testResult.MemoryUsageBytes, baseTestResult.MemoryUsageBytes):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"内存峰值: {BytesToMB(testResult.PeakMemoryUsageBytes):0.00} MB vs {BytesToMB(baseTestResult.PeakMemoryUsageBytes):0.00} MB " +
                $"({CalculateDifference(testResult.PeakMemoryUsageBytes, baseTestResult.PeakMemoryUsageBytes):+0.00;-0.00;0.00}%)");
        }

        // 显示测试结果
        private void DisplayTestResult(TestResult result)
        {
            Console.WriteLine($"总耗时: {result.TotalTimeMs} ms");
            Console.WriteLine($"平均耗时: {1000 * result.AverageTimeMs:0.00} ms/1000操作");
            Console.WriteLine($"内存占用: {BytesToMB(result.MemoryUsageBytes):0.00} MB");
            Console.WriteLine($"内存峰值: {BytesToMB(result.PeakMemoryUsageBytes):0.00} MB");
            Console.WriteLine($"测试日期: {result.TestDate}");
        }

        // 计算差异百分比
        private static double CalculateDifference(double current, double baseValue)
        {
            if (baseValue == 0) return 0;
            return (current - baseValue) / baseValue * 100;
        }

        // 字节转换为MB
        private static double BytesToMB(long bytes)
        {
            return bytes / (1024.0 * 1024.0);
        }

        public static void TestAll(string rankingListClassName, string? baseTestName = null)
        {
            string[] testList = Directory.GetFiles("Test", "*.json");
            foreach (string test in testList)
            {
                string testName = Path.GetFileNameWithoutExtension(test);
                TestOperator testOperator = new(rankingListClassName, testName, baseTestName);
                testOperator.Test();
                GC.Collect();
            }
        }
    }
}
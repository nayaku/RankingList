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
            string testResultDir = $"TestResults/{_rankingListClassName}";
            if (!Directory.Exists(testResultDir))
            {
                Directory.CreateDirectory(testResultDir);
            }

            // 准备测试
            Console.WriteLine($"== Test {_testName} ===");
            TestData testData = LoadTestData();
            int initialUserNum = testData.Users.Count;
            List<User> users = testData.Users;
            List<TestOperation> operations = testData.Operations;
            int operationCount = operations.Count;
            TestOperationType? limitOperationType = testData.LimitOperationType;
            testData = null; // 释放测试数据内存
            // 创建排行榜实例
            IRankingList rankingList = RankingListHelper.NewRankingList(_rankingListClassName, users);
            users = null; // 释放测试数据内存
            GC.Collect();
            // 开始内存监控
            long initialMemoryUsage = GC.GetTotalMemory(true);
            _peakMemoryUsage = initialMemoryUsage;
            _cancellationTokenSource = new CancellationTokenSource();
            Thread memoryMonitorThread = new(MonitorMemoryUsage) { IsBackground = true };
            memoryMonitorThread.Start();

            // 运行测试
            (List<OperationResult> operationResults, Stopwatch stopwatch) = RunTest(rankingList, operations);
            // 停止内存监控
            Thread.Sleep(100); // 等待内存监控线程更新峰值
            _cancellationTokenSource.Cancel();
            memoryMonitorThread.Join();

            // 计算测试结果
            TestResult testResultObj = new()
            {
                TestName = _testName,
                RankingListName = _rankingListClassName,
                InitUserNum = initialUserNum,
                LimitOperationType = limitOperationType,
                OperationNum = operationCount,
                RankingListUserNum = rankingList.GetRankingCount(),
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                AverageTimeMs = stopwatch.ElapsedMilliseconds / (double)operations.Count,
                MemoryUsageBytes = GC.GetTotalMemory(true) - initialMemoryUsage,
                PeakMemoryUsageBytes = _peakMemoryUsage - initialMemoryUsage,
                TestDate = DateTime.Now,
            };

            string testResultPath = $"{testResultDir}/{_testName}.json";
            using (FileStream fs = new(testResultPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(fs, testResultObj,
                    new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
            }

#if DEBUG
            rankingList.DebugPrint();
#endif
            if (_baseRankingListClassName != null)
            {
#if DEBUG
                ValidateResults(operationResults);
#endif
                CompareWithBase(testResultObj, _baseRankingListClassName, _testName);
            }
            else
            {
                DisplayTestResult(testResultObj);
            }
            Console.WriteLine($"== Test {_testName} End ===\n");
        }

        private TestData LoadTestData()
        {
            string testTargetDir = "Test";
            string testDataPath = $"{testTargetDir}/{_testName}.json";
            using FileStream fs = new(testDataPath, FileMode.Open, FileAccess.Read);
            TestData testData =
                JsonSerializer.Deserialize<TestData>(fs, new JsonSerializerOptions { IncludeFields = true }) ??
                throw new Exception($"无法加载测试数据 {testDataPath}");
            return testData;
        }

        private (List<OperationResult>, Stopwatch) RunTest(IRankingList rankingList, List<TestOperation> Operations)
        {
            List<OperationResult> operationResults = new(Operations.Count + 1);
            Stopwatch stopwatch = new();
            stopwatch.Start();
            foreach (TestOperation testOperation in Operations)
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
#if DEBUG
            string testResultDir = $"TestResults/{_rankingListClassName}";
            string operationResultPath = $"{testResultDir}/{_testName}_Operations.json";
            using (FileStream fs = new(operationResultPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(fs, operationResults,
                    new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
            }
#endif
#if !DEBUG
            operationResults = null;
#endif
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
                baseResults =
                    JsonSerializer.Deserialize<List<OperationResult>>(fs,
                        new JsonSerializerOptions { IncludeFields = true }) ??
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
        private static void CompareWithBase(TestResult testResult, string baseRankingListClassName, string testName)
        {
            TestResult baseTestResult = LoadTestResult(baseRankingListClassName, testName);

            Console.WriteLine($"对比基准测试: {baseRankingListClassName} {testName}");
            Console.WriteLine($"初始用户数: {testResult.InitUserNum}");
            Console.WriteLine($"限制操作类型: {testResult.LimitOperationType}");
            Console.WriteLine($"操作数: {testResult.OperationNum}");
            Console.WriteLine($"排名列表用户数: {testResult.RankingListUserNum} vs {baseTestResult.RankingListUserNum}");
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
            Console.WriteLine($"测试日期: {testResult.TestDate} vs {baseTestResult.TestDate}");
        }

        private static TestResult LoadTestResult(string rankingListClassName, string testName)
        {
            string testResultDirPath = $"TestResults/{rankingListClassName}";
            string testResultPath = $"{testResultDirPath}/{testName}.json";
            TestResult testResult;
            using (FileStream fs = new(testResultPath, FileMode.Open, FileAccess.Read))
            {
                testResult =
                    JsonSerializer.Deserialize<TestResult>(fs, new JsonSerializerOptions { IncludeFields = true }) ??
                    throw new Exception($"无法加载测试数据 {testResultPath}");
            }
            return testResult;
        }

        // 显示测试结果
        private static void DisplayTestResult(TestResult result)
        {
            Console.WriteLine($"测试: {result.RankingListName} {result.TestName}"); 
            Console.WriteLine($"初始用户数: {result.InitUserNum}");
            Console.WriteLine($"限制操作类型: {result.LimitOperationType}");
            Console.WriteLine($"操作数: {result.OperationNum}");
            Console.WriteLine($"排名列表用户数: {result.RankingListUserNum}");
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

        public static void Test(string rankingListClassName, string testName, string? baseTestName = null)
        {
            TestOperator testOperator = new(rankingListClassName, testName, baseTestName);
            testOperator.Test();
        }

        public static void TestAll(string rankingListClassName, string? baseTestName = null)
        {
            Console.WriteLine($"测试类: {rankingListClassName}");
            if (baseTestName != null)
            {
                Console.WriteLine($"基准测试类: {baseTestName}");
            }

            string[] testList = Directory.GetFiles("Test", "*.json");
            foreach (string test in testList)
            {
                string testName = Path.GetFileNameWithoutExtension(test);
                Test(rankingListClassName, testName, baseTestName);
                GC.Collect();
            }
        }

        public static void CompareAllWithBase(string rankingListClassName, string? baseTestName = null)
        {
            Console.WriteLine($"测试类: {rankingListClassName}");
            if (baseTestName != null)
            {
                Console.WriteLine($"基准测试类: {baseTestName}");
            }

            string[] testList = Directory.GetFiles("Test", "*.json");
            foreach (string test in testList)
            {
                string testName = Path.GetFileNameWithoutExtension(test);
                TestResult testResult = LoadTestResult(rankingListClassName, testName);
                if (baseTestName != null)
                {
                    CompareWithBase(testResult, baseTestName, testName);
                }
                else
                {
                    DisplayTestResult(testResult);
                }
            }
        }
    }
}
using RankingListNew;
using System.Diagnostics;
using System.Text.Json;

namespace RankingListTestNew
{
    // 测试结果类
    public class TestResult
    {
        public string RankingListName { get; set; }
        public long TotalTimeMs { get; set; }
        public double AverageTimeMs { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long PeakMemoryUsageBytes { get; set; }
        public DateTime TestDate { get; set; }
    }

    public struct OperationResult
    {
        public int Id;
        public OperationType OperationType;
        public List<RankingListResponse> RankingListResponses;
    }

    public class TestOperator
    {
        private static readonly DateTime InitialUserCreateTime = new(2056, 1, 1);
        private long _peakMemoryUsage;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly string _testName;
        private readonly string _rankingListClassName;
        private readonly string? _baseTestName;

        public TestOperator(string rankingListClassName, string testName, string? baseTestName)
        {
            _rankingListClassName = rankingListClassName;
            _testName = testName;
            _baseTestName = baseTestName;
        }

        public void Test()
        {
            Console.WriteLine($"== Test {_testName} ===");
            TestData testData = LoadTestData();
            IRankingList rankingList = RankingListHelper.NewRankingList(_rankingListClassName);
            GC.Collect();
            // 开始内存监控
            long initialMemoryUsage = GC.GetTotalMemory(true);
            _peakMemoryUsage = initialMemoryUsage;
            _cancellationTokenSource = new CancellationTokenSource();
            var memoryMonitorThread = new Thread(MonitorMemoryUsage) { IsBackground = true };
            memoryMonitorThread.Start();
            // 运行测试
            var (operationResults, stopwatch) = RunTest(rankingList, testData);
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
            if (_baseTestName != null)
            {
                CompareWithBase(testResultObj);
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
            TestData testData = JsonSerializer.Deserialize<TestData>(fs) ??
                                throw new Exception($"无法加载测试数据 {testDataPath}");
            return testData;
        }

        private (List<OperationResult>, Stopwatch) RunTest(IRankingList rankingList, TestData testData)
        {
            List<OperationResult> operationResults = new(testData.Operations.Count);
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
                    case OperationType.AddUser:
                    {
                        var user = new User(testOperation.UserId, testOperation.ScoreOrN,
                            InitialUserCreateTime.AddSeconds(testOperation.Id));
                        operationResult.RankingListResponses = [rankingList.AddUser(user)];
                        break;
                    }
                    case OperationType.UpdateUser:
                    {
                        var user = new User(testOperation.UserId, testOperation.ScoreOrN,
                            InitialUserCreateTime.AddSeconds(testOperation.Id));
                        operationResult.RankingListResponses = [rankingList.UpdateUser(user)];
                        break;
                    }

                    case OperationType.GetUserRank:
                        operationResult.RankingListResponses = [rankingList.GetUserRank(testOperation.Id)];
                        break;
                    case OperationType.GetTopN:
                        operationResult.RankingListResponses = rankingList.GetTopN(testOperation.ScoreOrN);
                        break;
                    case OperationType.GetAroundUser:
                        operationResult.RankingListResponses =
                            rankingList.GetAroundUser(testOperation.Id, testOperation.ScoreOrN);
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
                JsonSerializer.Serialize(fs, testResultObj);
            }

            string operationResultPath = $"{testResultDir}/{_testName}_Operations.json";
            using (FileStream fs = new(operationResultPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(fs, operationResults);
            }
        }

        /// <summary>
        /// 验证测试结果与基准
        /// </summary>
        /// <param name="testResults"></param>
        private void ValidateResults(List<OperationResult> testResults)
        {
            var baseTestResultDirPath = $"TestResults/{_rankingListClassName}";
            var baseTestResultPath = $"{baseTestResultDirPath}/{_baseTestName}_Operations.json";
            List<OperationResult> baseResults;
            using (FileStream fs = new(baseTestResultPath, FileMode.Open, FileAccess.Read))
            {
                baseResults = JsonSerializer.Deserialize<List<OperationResult>>(fs) ??
                              throw new Exception($"无法加载基准测试数据 {baseTestResultPath}");
            }

            if (baseResults.Count != testResults.Count)
            {
                throw new Exception("基准测试数据与测试数据操作数不一致");
            }

            int errorCount = 0;
            for (int i = 0; i < baseResults.Count; i++)
            {
                var baseResult = baseResults[i];
                var testResult = testResults[i];
                if (baseResult.OperationType != testResult.OperationType)
                {
                    Console.WriteLine($"基准测试数据与测试数据操作类型不一致，第{i}个操作");
                    errorCount++;
                }

                if (baseResult.RankingListResponses.Count != testResult.RankingListResponses.Count)
                {
                    Console.WriteLine($"基准测试数据与测试数据响应不一致，第{i}个操作");
                    errorCount++;
                }

                for (int j = 0; j < baseResult.RankingListResponses.Count; j++)
                {
                    if (baseResult.RankingListResponses[j].Rank != testResult.RankingListResponses[j].Rank ||
                        baseResult.RankingListResponses[j].User != testResult.RankingListResponses[j].User)
                    {
                        Console.WriteLine($"基准测试数据与测试数据响应不一致，第{i}个操作，第{j}个响应");
                        errorCount++;
                    }
                }
            }

            Console.WriteLine(errorCount > 0
                ? $"测试数据与基准数据不一致，共{errorCount}个错误"
                : "√ 所有操作结果验证通过！");
        }

        /// <summary>
        /// 对比测试结果与基准
        /// </summary>
        /// <param name="testResult"></param>
        private void CompareWithBase(TestResult testResult)
        {
            var baseTestResultDirPath = $"TestResults/{_rankingListClassName}";
            var baseTestResultPath = $"{baseTestResultDirPath}/{_baseTestName}.json";
            TestResult baseTestResult;
            using (FileStream fs = new(baseTestResultPath, FileMode.Open, FileAccess.Read))
            {
                baseTestResult = JsonSerializer.Deserialize<TestResult>(fs) ??
                                 throw new Exception($"无法加载基准测试数据 {baseTestResultPath}");
            }

            Console.WriteLine(
                $"总耗时: {testResult.TotalTimeMs} ms vs {baseTestResult.TotalTimeMs} ms " +
                $"({CalculateDifference(testResult.TotalTimeMs, baseTestResult.TotalTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"平均耗时: {1000 * testResult.AverageTimeMs:0.00} ms/1000操作 vs {1000 * baseTestResult.AverageTimeMs:0.00} ms/1000操作 " +
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
            var testList = Directory.GetFiles("Test", "*.json");
            foreach (var test in testList)
            {
                var testName = Path.GetFileNameWithoutExtension(test);
                var testOperator = new TestOperator(rankingListClassName, testName, baseTestName);
                testOperator.Test();
                GC.Collect();
            }
        }
    }
}

using RankingList;
using System.Diagnostics;
using System.Text.Json;

namespace RankingListTest
{
    // 操作类型枚举
    public enum OperationType
    {
        AddUser,
        UpdateUser,
        GetUserRank,
        GetTopN,
        GetAroundUser
    }

    // 测试操作类
    public class TestOperation
    {
        public int Id { get; set; }
        public OperationType Type { get; set; }
        public int UserId { get; set; }
        public int Score { get; set; }
        public int TopN { get; set; }
        public int AroundN { get; set; }
    }

    // 测试初始类
    public class TestInitial
    {
        public List<IUser> Users { get; set; }
        public List<TestOperation> AllOperations { get; set; }
        public List<List<TestOperation>> SingleOperations { get; set; }
    }

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

    // 操作结果类
    public class OperationResult
    {
        public int Id { get; set; }
        public OperationType Type { get; set; }
        public RankingListResponse[]? UserRankResult { get; set; }
    }

    // 单项操作结果类
    public class SingleOperationResult
    {
        public OperationType Type { get; set; }
        public int OperationCount { get; set; }
        public long TotalTimeMs { get; set; }
        public double AverageTimeMs { get; set; }
    }

    // 基准结果集合类
    public class BenchmarkResults
    {
        public List<OperationResult> Results { get; set; }
        public TestResult PerformanceResult { get; set; }
        public List<SingleOperationResult> SingleOperationResults { get; set; }
    }

    public class RankingListTestCore
    {
        private const string BaseOperationResultsFilePath = "base_operation_results.json";
        private const string TestInitialFilePath = "test_initial.json";

        private const int InitialUserCount = 1_0000;
        private static readonly DateTime InitialUserCreateTime = new(2026, 1, 1);
        private int currentUserId = InitialUserCount + 1;
        private int currentOperationId = 0;
        private Dictionary<int, int> userIdToScore;
        private const int allOperationNum = 100_0000;
        private static readonly double[] singleOperationNumProportions = [0.1, 0.2, 0.3, 0.2, 0.2];

        private Process _process = Process.GetCurrentProcess();
        private long _peakMemoryUsage;

        // 生成初始用户数据
        public List<IUser> GenerateInitialUsers(Random random)
        {
            userIdToScore = [];
            var users = new List<IUser>(InitialUserCount);
            for (int i = 0; i < InitialUserCount; i++)
            {
                var user = new User
                {
                    Id = i + 1,
                    Score = GeneratePowerLawScore(random),
                    LastActive = InitialUserCreateTime.AddSeconds(i)
                };
                userIdToScore[user.Id] = user.Score;
                users.Add(user);
            }

            return users;
        }

        // 生成操作列表
        public List<TestOperation> GenerateOperations(Random random, int operationNum,
            OperationType? specifiedType = null)
        {
            var operations = new List<TestOperation>(operationNum);
            for (int i = 0; i < operationNum; i++)
            {
                var operation = new TestOperation
                {
                    Id = ++currentOperationId,
                };
                if (specifiedType == null)
                {
                    double operationType = random.NextDouble();
                    operation.Type = operationType switch
                    {
                        // 10% AddUser
                        < 0.1 => OperationType.AddUser,
                        // 20% UpdateUser
                        < 0.3 => OperationType.UpdateUser,
                        // 30% GetUserRank
                        < 0.6 => OperationType.GetUserRank,
                        // 20% GetTopN
                        < 0.8 => OperationType.GetTopN,
                        // 20% GetAroundUser
                        _ => OperationType.GetAroundUser
                    };
                }
                else
                {
                    operation.Type = specifiedType.Value;
                }

                switch (operation.Type)
                {
                    case OperationType.AddUser:
                        operation.UserId = currentUserId++;
                        operation.Score = GeneratePowerLawScore(random, 100);
                        userIdToScore[operation.UserId] = operation.Score;
                        break;
                    case OperationType.UpdateUser:
                    {
                        operation.UserId = random.Next(1, currentUserId);
                        int score = userIdToScore[operation.UserId];
                        operation.Score = score + GeneratePowerLawScore(random, 100);
                        break;
                    }
                    case OperationType.GetUserRank:
                        operation.UserId = random.Next(1, currentUserId);
                        break;
                    case OperationType.GetTopN:
                        operation.TopN = random.Next(1, 100);
                        operation.UserId = random.Next(1, currentUserId);
                        break;
                    case OperationType.GetAroundUser:
                        operation.UserId = random.Next(1, currentUserId);
                        operation.AroundN = random.Next(1, 10);
                        break;
                }

                operations.Add(operation);
            }

            return operations;
        }

        // 生成幂律分布的分数
        private static int GeneratePowerLawScore(Random random, int maxScore = 1000000)
        {
            // 简单的幂律分布生成
            double uniform = random.NextDouble();
            return (int)(Math.Pow(uniform, 2) * maxScore);
        }

        // 生成初始化测试数据
        public void GenerateTestInitialData()
        {
            Random random = new(42);
            // 生成初始用户数据
            List<IUser> initialUsers = GenerateInitialUsers(random);
            // 生成操作列表
            List<TestOperation> operations = GenerateOperations(random, allOperationNum);
            // 生成单项测试的操作列表
            var singleOperations = Enum.GetValues<OperationType>()
                .Select((operationType, index) =>
                    GenerateOperations(random, (int)(allOperationNum * singleOperationNumProportions[index]), operationType))
                .ToList();

            TestInitial testInitial = new()
            {
                Users = initialUsers,
                AllOperations = operations,
                SingleOperations = singleOperations
            };
            // 储存测试数据
            using FileStream fs = new(TestInitialFilePath, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(fs, testInitial, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"测试数据已生成并保存到 {TestInitialFilePath}");
        }

        // 监控内存使用情况
        private void MonitorMemoryUsage()
        {
            while (true)
            {
                _process.Refresh();
                long currentMemory = _process.WorkingSet64;
                if (currentMemory > _peakMemoryUsage)
                {
                    _peakMemoryUsage = currentMemory;
                }

                Thread.Sleep(10);
            }
        }

        // 执行操作
        private (List<OperationResult>, Stopwatch) ExecuteOperations(IRankingList rankingList,
            List<TestOperation> operations)
        {
            GC.Collect();
            var operationResults = new List<OperationResult>(operations.Count);
            var stopwatch = Stopwatch.StartNew();

            foreach (var operation in operations)
            {
                var opResult = new OperationResult
                {
                    Id = operation.Id,
                    Type = operation.Type
                };

                switch (operation.Type)
                {
                    case OperationType.AddUser:
                    {
                        var user = new User
                        {
                            Id = operation.UserId,
                            Score = operation.Score,
                            LastActive = InitialUserCreateTime.AddSeconds(InitialUserCount + operation.Id)
                        };
                        var addResult = rankingList.AddUser(user);
                        opResult.UserRankResult = [addResult];
                    }
                        break;
                    case OperationType.UpdateUser:
                    {
                        var user = new User
                        {
                            Id = operation.UserId,
                            Score = operation.Score,
                            LastActive = InitialUserCreateTime.AddSeconds(InitialUserCount + operation.Id)
                        };
                        var updateResult = rankingList.UpdateUser(user);
                        opResult.UserRankResult = [updateResult];
                    }
                        break;
                    case OperationType.GetUserRank:
                        var rankResult = rankingList.GetUserRank(operation.UserId);
                        opResult.UserRankResult = [rankResult];
                        break;
                    case OperationType.GetTopN:
                        var topNResult = rankingList.GetTopN(operation.TopN);
                        opResult.UserRankResult = topNResult;
                        break;
                    case OperationType.GetAroundUser:
                        var aroundUserResult = rankingList.GetAroundUser(operation.UserId, operation.AroundN);
                        opResult.UserRankResult = aroundUserResult;
                        break;
                }

                operationResults.Add(opResult);
            }

            stopwatch.Stop();

            return (operationResults, stopwatch);
        }

        // 计算单项操作结果
        private List<SingleOperationResult> CalculateSingleOperationResults(List<List<TestOperation>> operationss,
            IRankingList rankingList)
        {
            var singleOperationResults = new List<SingleOperationResult>();

            foreach (var operations in operationss)
            {
                int operationCount = operations.Count;
                #if DEBUG
                Debug.Assert(operations.All(op=>op.Type == operations[0].Type), "所有操作类型必须相同");
                #endif

                var (_, singleOpStopwatch) =
                    ExecuteOperations(rankingList, operations);
                double averageTimeMs = singleOpStopwatch.ElapsedMilliseconds / (double)operationCount;

                singleOperationResults.Add(new SingleOperationResult
                {
                    Type = operations[0].Type,
                    OperationCount = operationCount,
                    TotalTimeMs = singleOpStopwatch.ElapsedMilliseconds,
                    AverageTimeMs = averageTimeMs
                });
            }

            return singleOperationResults;
        }

        // 处理基准测试逻辑
        private void HandleBenchmarkLogic(BenchmarkResults benchmarkResults, string rankingListName,
            BenchmarkResults? baseBenchmarkResults = null)
        {
            // 如果是基准测试，保存操作结果
            if (baseBenchmarkResults == null)
            {
                using FileStream fs = new(BaseOperationResultsFilePath, FileMode.Create, FileAccess.Write);
                JsonSerializer.Serialize(fs, benchmarkResults, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"基准操作结果已保存到 {BaseOperationResultsFilePath}");
                Console.WriteLine("\n=== 基准测试结果 ===");
                DisplayTestResult(benchmarkResults.PerformanceResult);
            }
            else
            {
                ValidateResults(baseBenchmarkResults.Results, benchmarkResults.Results);
                string testResultFilePath = $"{rankingListName}_test_results.json";
                using (FileStream fs = new(testResultFilePath, FileMode.Create, FileAccess.Write))
                    JsonSerializer.Serialize(fs, benchmarkResults,
                        new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"测试操作结果已保存到 {testResultFilePath}");
                Console.WriteLine("\n=== 测试结果 ===");
                DisplayTestResult(benchmarkResults.PerformanceResult);
                CompareWithBase(baseBenchmarkResults.PerformanceResult, benchmarkResults.PerformanceResult);
            }
        }

        // 运行测试
        public TestResult RunTest(string rankingListName, bool isBenchmark = false)
        {
            // 加载测试数据
            TestInitial testInitial;
            using (FileStream fs = new(TestInitialFilePath, FileMode.Open, FileAccess.Read))
                testInitial = JsonSerializer.Deserialize<TestInitial>(fs) ??
                              throw new Exception("无法加载测试数据");
            Console.WriteLine($"初始用户数: {testInitial.Users.Count}");
            Console.WriteLine($"操作数: {testInitial.AllOperations.Count}");

            // 创建排行榜实例
            var rankingList = DllMain.CreateRankingList([.. testInitial.Users], rankingListName);

            GC.Collect();
            // 开始内存监控
            _peakMemoryUsage = 0;
            var memoryMonitorThread = new Thread(MonitorMemoryUsage) { IsBackground = true };
            memoryMonitorThread.Start();

            // 执行所有操作
            var (operationResults, stopwatch) = ExecuteOperations(rankingList, testInitial.AllOperations);

            // 停止计时和内存监控
            Thread.Sleep(100); // 等待内存监控线程更新峰值

            // 收集测试结果
            var result = new TestResult
            {
                RankingListName = rankingListName,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                AverageTimeMs = stopwatch.ElapsedMilliseconds / (double)testInitial.AllOperations.Count,
                MemoryUsageBytes = _process.WorkingSet64,
                PeakMemoryUsageBytes = _peakMemoryUsage,
                TestDate = DateTime.Now
            };

            // 计算单项操作结果
            var singleOperationResults =
                CalculateSingleOperationResults(testInitial.SingleOperations, rankingList);

            var benchmarkResults = new BenchmarkResults
            {
                Results = operationResults,
                PerformanceResult = result,
                SingleOperationResults = singleOperationResults
            };

            // 加载基准测试结果（如果不是基准测试）
            BenchmarkResults? baseBenchmarkResults = null;
            if (!isBenchmark)
            {
                using FileStream fs = new(BaseOperationResultsFilePath, FileMode.Open, FileAccess.Read);
                baseBenchmarkResults = JsonSerializer.Deserialize<BenchmarkResults>(fs) ??
                                       throw new Exception("无法加载基准操作结果");
            }

            // 处理基准测试逻辑
            HandleBenchmarkLogic(benchmarkResults, rankingListName, baseBenchmarkResults);

            // 运行单项操作测试
            RunSingleOperationTest(singleOperationResults, baseBenchmarkResults?.SingleOperationResults);

#if DEBUG
            //if(rankingList is BucketRankingList bucketRankingList)
            //    bucketRankingList.DebugPrint(); 

            if (rankingList is BucketRankingList2 bucketRankingList2)
                bucketRankingList2.DebugPrint();
            if (rankingList is TreeBucketRankingList treeBucketRankingList)
                treeBucketRankingList.DebugPrint();
            if (rankingList is TreeBucketRankingList2 treeBucketRankingList2)
                treeBucketRankingList2.DebugPrint();
            if (rankingList is TreeAVLBucketRankingList treeAVLBucketRankingList)
                treeAVLBucketRankingList.DebugPrint();
#endif
            return result;
        }

        // 验证测试结果与基准
        public void ValidateResults(List<OperationResult> baseResults, List<OperationResult> testResults)
        {
            Console.WriteLine("\n=== 验证操作结果与基准对比 ===");

            int totalOperations = testResults.Count;
            int failedOperations = 0;

            // 逐一比较测试结果与基准结果
            for (int i = 0; i < totalOperations; i++)
            {
                if (i >= baseResults.Count)
                {
                    Console.WriteLine($"操作 {testResults[i].Id}: 测试结果中缺少该操作");
                    failedOperations++;
                    continue;
                }

                var testResult = testResults[i];
                var baseResult = baseResults[i];

                if (!CompareOperationResults(testResult, baseResult))
                {
                    failedOperations++;
                    Console.WriteLine($"操作 {testResult.Id}: {testResult.Type} 结果不匹配");
                }
            }

            Console.WriteLine(failedOperations > 0
                ? $"验证失败，{failedOperations} 个操作结果不匹配"
                : "√ 所有操作结果验证通过！");
        }

        // 对比操作结果
        private bool CompareOperationResults(OperationResult testResult, OperationResult baseResult)
        {
            if (testResult.Type != baseResult.Type)
                return false;

            return CompareRankResults(testResult.UserRankResult, baseResult.UserRankResult);
        }

        // 对比用户排名结果
        private bool CompareRankResults(RankingListResponse[]? testResults,
            RankingListResponse[]? baseResults)
        {
            if (testResults == null && baseResults == null)
                return true;
            if (testResults == null || baseResults == null)
                return false;
            if (testResults.Length != baseResults.Length)
                return false;
            for (int i = 0; i < testResults.Length; i++)
            {
                if (!CompareRankResult(testResults[i], baseResults[i]))
                    return false;
            }

            return true;
        }

        // 对比排名结果
        private bool CompareRankResult(RankingListResponse? testResult,
            RankingListResponse? baseResult)
        {
            if (testResult == null && baseResult == null)
                return true;
            if (testResult == null || baseResult == null)
                return false;
            if (testResult.Rank != baseResult.Rank)
                return false;
            if (testResult.User == null && baseResult.User == null)
                return true;
            if (testResult.User == null || baseResult.User == null)
                return false;

            if (testResult.User is not User testUser || baseResult.User is not User baseUser)
                return false;
            if (testUser.Id != baseUser.Id)
                return false;
            if (testUser.Score != baseUser.Score)
                return false;
            return true;
        }

        // 对比测试结果与基准
        public void CompareWithBase(TestResult baseResult, TestResult testResult)
        {
            Console.WriteLine($"\n=== 与基准 {baseResult.RankingListName} 的对比 ===");
            Console.WriteLine(
                $"总耗时: {testResult.TotalTimeMs} ms vs {baseResult.TotalTimeMs} ms " +
                $"({CalculateDifference(testResult.TotalTimeMs, baseResult.TotalTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"平均耗时: {1000 * testResult.AverageTimeMs:0.00} ms/1000操作 vs {1000 * baseResult.AverageTimeMs:0.00} ms/1000操作 " +
                $"({CalculateDifference(1000 * testResult.AverageTimeMs, 1000 * baseResult.AverageTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"内存占用: {BytesToMB(testResult.MemoryUsageBytes):0.00} MB vs {BytesToMB(baseResult.MemoryUsageBytes):0.00} MB " +
                $"({CalculateDifference(testResult.MemoryUsageBytes, baseResult.MemoryUsageBytes):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"内存峰值: {BytesToMB(testResult.PeakMemoryUsageBytes):0.00} MB vs {BytesToMB(baseResult.PeakMemoryUsageBytes):0.00} MB " +
                $"({CalculateDifference(testResult.PeakMemoryUsageBytes, baseResult.PeakMemoryUsageBytes):+0.00;-0.00;0.00}%)");
        }

        // 运行单项操作测试
        public void RunSingleOperationTest(List<SingleOperationResult> singleResults,
            List<SingleOperationResult>? baseSingleResults = null)
        {
            Console.WriteLine("\n=== 单项操作耗时测试 ===\n");

            // 输出每种操作类型的测试结果
            foreach (var result in singleResults)
            {
                Console.WriteLine($"【{result.Type}】");

                // 与基准对比
                if (baseSingleResults != null)
                {
                    var baseResult = baseSingleResults.FirstOrDefault(r => r.Type == result.Type);
                    if (baseResult != null)
                    {
                        double totalTimeDifference = CalculateDifference(result.TotalTimeMs, baseResult.TotalTimeMs);
                        double avgTimeDifference = CalculateDifference(1000 * result.AverageTimeMs,
                            1000 * baseResult.AverageTimeMs);
                        Console.WriteLine($"  操作数: {result.OperationCount} vs {baseResult.OperationCount}");
                        Console.WriteLine(
                            $"  总耗时: {result.TotalTimeMs} ms vs {baseResult.TotalTimeMs} ms " +
                            $"({totalTimeDifference:+0.00;-0.00;0.00}%) ({result.OperationCount}次操作)");
                        Console.WriteLine(
                            $"  平均耗时: {1000 * result.AverageTimeMs:0.00} ms/1000操作 vs {1000 * baseResult.AverageTimeMs:0.00} ms/1000操作 " +
                            $"({avgTimeDifference:+0.00;-0.00;0.00}%)");
                    }
                }
                else
                {
                    // 没有基准时的输出
                    Console.WriteLine($"  操作数: {result.OperationCount}");
                    Console.WriteLine($"  总耗时: {result.TotalTimeMs} ms ({result.OperationCount}次操作)");
                    Console.WriteLine($"  平均耗时: {1000 * result.AverageTimeMs:0.0000} ms/1000操作");
                }
            }
        }

        // 计算差异百分比
        private double CalculateDifference(double current, double baseValue)
        {
            if (baseValue == 0) return 0;
            return (current - baseValue) / baseValue * 100;
        }

        // 字节转换为MB
        private double BytesToMB(long bytes)
        {
            return bytes / (1024.0 * 1024.0);
        }

        // 显示测试结果
        public void DisplayTestResult(TestResult result)
        {
            Console.WriteLine($"排行榜名称: {result.RankingListName}");
            Console.WriteLine($"总耗时: {result.TotalTimeMs} ms");
            Console.WriteLine($"平均耗时: {1000 * result.AverageTimeMs:0.00} ms/1000操作");
            Console.WriteLine($"内存占用: {BytesToMB(result.MemoryUsageBytes):0.00} MB");
            Console.WriteLine($"内存峰值: {BytesToMB(result.PeakMemoryUsageBytes):0.00} MB");
            Console.WriteLine($"测试日期: {result.TestDate}");
        }
    }
}
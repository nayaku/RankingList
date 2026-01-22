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

    // 基准结果集合类
    public class BenchmarkResults
    {
        public List<OperationResult> Results { get; set; }
        public TestResult PerformanceResult { get; set; }
    }

    public class RankingListTestCore
    {
        private const string OperationsFilePath = "operations.json";
        private const string BaseOperationResultsFilePath = "base_operation_results.json";
        private const string InitialUsersFilePath = "initial_users.json";
        private const int InitialUserCount = 1_0000;
        private static readonly DateTime InitialUserCreateTime = new(2026, 1, 1);
        private int CurrentUserId = InitialUserCount + 1;
        private Dictionary<int, int> UserIdToScore;
        private const int TotalOperations = 100_0000;

        private Process _process = Process.GetCurrentProcess();
        private long _peakMemoryUsage;

        // 生成操作列表
        public void GenerateOperations()
        {
            var random = new Random(42); // 固定种子，保证每次生成的操作列表一致
            var operations = new List<TestOperation>();

            for (int i = 0; i < TotalOperations; i++)
            {
                double operationType = random.NextDouble();
                var operation = new TestOperation
                {
                    Id = i + 1,
                };

                if (operationType < 0.1) // 10% AddUser
                {
                    operation.Type = OperationType.AddUser;
                    operation.UserId = CurrentUserId++;
                    operation.Score = GeneratePowerLawScore(random, 100);
                    UserIdToScore[operation.UserId] = operation.Score;
                }
                else if (operationType < 0.3) // 20% UpdateUser
                {
                    operation.Type = OperationType.UpdateUser;
                    operation.UserId = random.Next(1, CurrentUserId);
                    int score = UserIdToScore[operation.UserId];
                    operation.Score = score + GeneratePowerLawScore(random, 100);
                }
                else if (operationType < 0.6) // 30% GetUserRank
                {
                    operation.Type = OperationType.GetUserRank;
                    operation.UserId = random.Next(1, CurrentUserId);
                }
                else if (operationType < 0.8) // 20% GetTopN
                {
                    operation.Type = OperationType.GetTopN;
                    operation.TopN = random.Next(1, 100);
                    operation.UserId = random.Next(1, CurrentUserId);
                }
                else // 20% GetAroundUser
                {
                    operation.Type = OperationType.GetAroundUser;
                    operation.UserId = random.Next(1, CurrentUserId);
                    operation.AroundN = random.Next(1, 10);
                }

                operations.Add(operation);
            }

            // 保存操作列表到JSON文件
            using FileStream fs = new(OperationsFilePath, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(fs, operations, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"操作列表已生成并保存到 {OperationsFilePath}");
        }

        // 生成初始用户数据
        public User[] GenerateInitialUsers()
        {
            UserIdToScore = [];
            var users = new User[InitialUserCount];
            var random = new Random(42);

            for (int i = 0; i < InitialUserCount; i++)
            {
                users[i] = new User
                {
                    Id = i + 1,
                    Score = GeneratePowerLawScore(random),
                    LastActive = InitialUserCreateTime.AddSeconds(i)
                };
                UserIdToScore[users[i].Id] = users[i].Score;
            }

            // 保存初始用户到JSON文件
            using FileStream fs = new(InitialUsersFilePath, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(fs, users, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"初始用户数据已生成并保存到 {InitialUsersFilePath}");

            return users;
        }

        // 生成幂律分布的分数
        private static int GeneratePowerLawScore(Random random, int maxScore = 1000000)
        {
            // 简单的幂律分布生成
            double uniform = random.NextDouble();
            return (int)(Math.Pow(uniform, 2) * maxScore);
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

        // 运行测试
        public TestResult RunTest(string rankingListName, bool isBenchmark = false)
        {
            List<TestOperation> operations;
            // 加载操作列表
            using (FileStream fs1 = new(OperationsFilePath, FileMode.Open, FileAccess.Read))
            {
                operations = JsonSerializer.Deserialize<List<TestOperation>>(fs1) ??
                             throw new Exception("无法加载操作列表");
            }

            int totalOperationCount = operations.Count;
            Console.WriteLine($"总操作数: {totalOperationCount}");
            // 加载初始用户数据
            User[] initialUsers;
            using (FileStream fs2 = new(InitialUsersFilePath, FileMode.Open, FileAccess.Read))
            {
                initialUsers = JsonSerializer.Deserialize<User[]>(fs2) ??
                               throw new Exception("无法加载初始用户数据");
            }

            int initialUserCount = initialUsers.Length;
            Console.WriteLine($"初始用户数: {initialUserCount}");
            // 创建排行榜实例
            var rankingList = DllMain.CreateRankingList(initialUsers, rankingListName);

            GC.Collect();
            // 开始内存监控
            _peakMemoryUsage = 0;
            var memoryMonitorThread = new Thread(MonitorMemoryUsage)
            {
                IsBackground = true
            };
            memoryMonitorThread.Start();

            // 开始测试计时
            var stopwatch = Stopwatch.StartNew();

            // 收集操作结果
            var operationResults = new List<OperationResult>(operations.Count);

            // 顺序执行所有操作
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

            // 停止计时和内存监控
            stopwatch.Stop();
            Thread.Sleep(100); // 等待内存监控线程更新峰值

            // 收集测试结果
            var result = new TestResult
            {
                RankingListName = rankingListName,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                AverageTimeMs = stopwatch.ElapsedMilliseconds / (double)operations.Count,
                MemoryUsageBytes = _process.WorkingSet64,
                PeakMemoryUsageBytes = _peakMemoryUsage,
                TestDate = DateTime.Now
            };

            var benchmarkResults = new BenchmarkResults
            {
                Results = operationResults,
                PerformanceResult = result
            };
            BenchmarkResults baseBenchmarkResults;
            // 如果是基准测试，保存操作结果
            if (isBenchmark)
            {
                using FileStream fs = new(BaseOperationResultsFilePath, FileMode.Create, FileAccess.Write);
                JsonSerializer.Serialize(fs, benchmarkResults, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"基准操作结果已保存到 {BaseOperationResultsFilePath}");
                Console.WriteLine("\n=== 基准测试结果 ===");
                DisplayTestResult(result);
            }
            else
            {
                // 验证测试结果与基准
                using (FileStream fs = new(BaseOperationResultsFilePath, FileMode.Open, FileAccess.Read))
                    baseBenchmarkResults = JsonSerializer.Deserialize<BenchmarkResults>(fs) ??
                                           throw new Exception("无法加载基准操作结果");
                ValidateResults(baseBenchmarkResults.Results, operationResults);
                string testResultFilePath = $"{rankingListName}_test_results.json";
                using (FileStream fs = new(testResultFilePath, FileMode.Create, FileAccess.Write))
                    JsonSerializer.Serialize(fs, benchmarkResults,
                        new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"测试操作结果已保存到 {testResultFilePath}");
                Console.WriteLine("\n=== 测试结果 ===");
                DisplayTestResult(result);
                CompareWithBase(baseBenchmarkResults.PerformanceResult, result);
            }

#if DEBUG
            //if(rankingList is BucketRankingList bucketRankingList)
            //    bucketRankingList.DebugPrint(); 

            if (rankingList is BucketRankingList2 bucketRankingList2)
                bucketRankingList2.DebugPrint();
            if (rankingList is TreeBucketRankingList treeBucketRankingList)
                treeBucketRankingList.DebugPrint();
#endif
            return result;
        }

        // 验证测试结果与基准
        public void ValidateResults(List<OperationResult> baseResults, List<OperationResult> testResults)
        {
            Console.WriteLine("\n=== 验证操作结果与基准对比 ===");

            int totalOperations = testResults.Count;
            int passedOperations = 0;
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

                if (CompareOperationResults(testResult, baseResult))
                {
                    passedOperations++;
                }
                else
                {
                    failedOperations++;
                    Console.WriteLine($"操作 {testResult.Id}: {testResult.Type} 结果不匹配");
                }
            }


            if (failedOperations > 0)
            {
                Console.WriteLine($"验证失败，{failedOperations} 个操作结果不匹配");
            }
            else
            {
                Console.WriteLine("√ 所有操作结果验证通过！");
            }
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

            var testUser = testResult.User as User;
            var baseUser = baseResult.User as User;

            if (testUser == null || baseUser == null)
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
                $"总耗时: {testResult.TotalTimeMs} ms vs {baseResult.TotalTimeMs} ms ({CalculateDifference(testResult.TotalTimeMs, baseResult.TotalTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"平均耗时: {1000 * testResult.AverageTimeMs:0.00} ms vs {1000 * baseResult.AverageTimeMs:0.00} ms ({CalculateDifference(1000 * testResult.AverageTimeMs, 1000 * baseResult.AverageTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"内存占用: {BytesToMB(testResult.MemoryUsageBytes):0.00} MB vs {BytesToMB(baseResult.MemoryUsageBytes):0.00} MB ({CalculateDifference(testResult.MemoryUsageBytes, baseResult.MemoryUsageBytes):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"内存峰值: {BytesToMB(testResult.PeakMemoryUsageBytes):0.00} MB vs {BytesToMB(baseResult.PeakMemoryUsageBytes):0.00} MB ({CalculateDifference(testResult.PeakMemoryUsageBytes, baseResult.PeakMemoryUsageBytes):+0.00;-0.00;0.00}%)");
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
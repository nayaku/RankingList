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
        private const string BaseResultFilePath = "base_result.json";
        private const string BaseOperationResultsFilePath = "base_operation_results.json";
        private const string InitialUsersFilePath = "initial_users.json";
        private const int InitialUserCount = 100_0000;
        private int CurrentUserId = InitialUserCount + 1;
        private Dictionary<int, int> UserIdToScore;
        private const int TotalOperations = 10000;

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
                    operation.UserId = random.Next(1, CurrentUserId + 1);
                    int score = UserIdToScore[operation.UserId];
                    operation.Score = score + GeneratePowerLawScore(random, 100);
                }
                else if (operationType < 0.6) // 30% GetUserRank
                {
                    operation.Type = OperationType.GetUserRank;
                    operation.UserId = random.Next(1, CurrentUserId + 1);
                }
                else if (operationType < 0.8) // 20% GetTopN
                {
                    operation.Type = OperationType.GetTopN;
                    operation.TopN = random.Next(1, 100);
                    operation.UserId = random.Next(1, CurrentUserId + 1);
                }
                else // 20% GetAroundUser
                {
                    operation.Type = OperationType.GetAroundUser;
                    operation.UserId = random.Next(1, CurrentUserId + 1);
                    operation.AroundN = random.Next(1, 10);
                }

                operations.Add(operation);
            }

            // 保存操作列表到JSON文件
            var json = JsonSerializer.Serialize(operations, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(OperationsFilePath, json);
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
                    LastActive = DateTime.Now
                };
                UserIdToScore[users[i].Id] = users[i].Score;
            }

            // 保存初始用户到JSON文件
            var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(InitialUsersFilePath, json);
            Console.WriteLine($"初始用户数据已生成并保存到 {InitialUsersFilePath}");

            return users;
        }

        // 生成幂律分布的分数
        private int GeneratePowerLawScore(Random random, int maxScore = 1000000)
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
            // 加载操作列表
            var operationsJson = File.ReadAllText(OperationsFilePath);
            var operations = JsonSerializer.Deserialize<List<TestOperation>>(operationsJson) ??
                             throw new Exception("无法加载操作列表");
            int totalOperationCount = operations.Count;
            Console.WriteLine($"总操作数: {totalOperationCount}");
            var initialUsersJson = File.ReadAllText(InitialUsersFilePath);
            var initialUsers = JsonSerializer.Deserialize<User[]>(initialUsersJson) ??
                               throw new Exception("无法加载初始用户数据");
            int initialUserCount = initialUsers.Length;
            Console.WriteLine($"初始用户数: {initialUserCount}");
            // 创建排行榜实例
            var rankingList = DllMain.CreateRankingList(initialUsers, rankingListName);

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
                                LastActive = DateTime.Now
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
                                LastActive = DateTime.Now
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
            // 如果是基准测试，保存操作结果
            if (isBenchmark)
            {
                SaveBaseOperationResults(benchmarkResults);
            }
            else
            {
                // 验证测试结果与基准
                ValidateResults(operationResults);
                string testResultFilePath = $"{rankingListName}_test_results.json";
                SaveTestOperationResults(benchmarkResults, testResultFilePath);
            }

            return result;
        }

        // 保存基准结果
        public void SaveBaseResult(TestResult result)
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BaseResultFilePath, json);
            Console.WriteLine($"基准结果已保存到 {BaseResultFilePath}");
        }

        // 加载基准结果
        public TestResult LoadBaseResult()
        {
            var json = File.ReadAllText(BaseResultFilePath);
            return JsonSerializer.Deserialize<TestResult>(json) ??
                   throw new Exception("无法加载基准结果");
        }

        // 保存基准操作结果
        public void SaveBaseOperationResults(BenchmarkResults results)
        {
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BaseOperationResultsFilePath, json);
            Console.WriteLine($"基准操作结果已保存到 {BaseOperationResultsFilePath}");
        }
        public void SaveTestOperationResults(BenchmarkResults results, string filePath)
        {
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            Console.WriteLine($"测试操作结果已保存到 {filePath}");
        }
        // 加载基准操作结果
        public BenchmarkResults LoadBaseOperationResults()
        {
            var json = File.ReadAllText(BaseOperationResultsFilePath);
            return JsonSerializer.Deserialize<BenchmarkResults>(json) ??
                   throw new Exception("无法加载基准操作结果");
        }

        // 验证测试结果与基准
        public void ValidateResults(List<OperationResult> testResults)
        {
            Console.WriteLine("\n=== 验证操作结果与基准对比 ===");

            // 加载基准操作结果
            var baseResults = LoadBaseOperationResults();

            int totalOperations = testResults.Count;
            int passedOperations = 0;
            int failedOperations = 0;

            // 逐一比较测试结果与基准结果
            for (int i = 0; i < totalOperations; i++)
            {
                if (i >= baseResults.Results.Count)
                {
                    Console.WriteLine($"操作 {testResults[i].Id}: 测试结果中缺少该操作");
                    failedOperations++;
                    continue;
                }

                var testResult = testResults[i];
                var baseResult = baseResults.Results[i];

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
        public void CompareWithBase(TestResult testResult)
        {
            var baseResult = LoadBaseResult();

            Console.WriteLine($"\n=== 与基准 {baseResult.RankingListName} 的对比 ===");
            Console.WriteLine(
                $"总耗时: {testResult.TotalTimeMs} ms vs {baseResult.TotalTimeMs} ms ({CalculateDifference(testResult.TotalTimeMs, baseResult.TotalTimeMs):+0.00;-0.00;0.00}%)");
            Console.WriteLine(
                $"平均耗时: {testResult.AverageTimeMs:0.00} ms vs {baseResult.AverageTimeMs:0.00} ms ({CalculateDifference(testResult.AverageTimeMs, baseResult.AverageTimeMs):+0.00;-0.00;0.00}%)");
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
            Console.WriteLine($"平均耗时: {result.AverageTimeMs:0.00} ms/操作");
            Console.WriteLine($"内存占用: {BytesToMB(result.MemoryUsageBytes):0.00} MB");
            Console.WriteLine($"内存峰值: {BytesToMB(result.PeakMemoryUsageBytes):0.00} MB");
            Console.WriteLine($"测试日期: {result.TestDate}");
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using RankingList;

namespace RankingListTest
{
    // 操作类型枚举
    public enum OperationType
    {
        GetRankingList,
        AddOrUpdateUser,
        GetUserRank
    }

    // 测试操作类
    public class TestOperation
    {
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
        public OperationType Type { get; set; }
        public int UserId { get; set; }
        public RankingListSingleResponse? UserRankResult { get; set; }
        public RankingListMutiResponse? RankingListResult { get; set; }
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
        private const int InitialUserCount = 1000000;
        private int CurrentUserId = InitialUserCount + 1;
        private Dictionary<int, int> UserIdToScore;
        private const int TotalOperations = 1000;

        private Process _process = Process.GetCurrentProcess();
        private long _peakMemoryUsage = 0;

        // 生成操作列表
        public void GenerateOperations()
        {
            var random = new Random(42); // 固定种子，保证每次生成的操作列表一致
            var operations = new List<TestOperation>();

            for (int i = 0; i < TotalOperations; i++)
            {
                double operationType = random.NextDouble();
                var operation = new TestOperation();

                if (operationType < 0.7) // 70% GetRankingList
                {
                    operation.Type = OperationType.GetRankingList;
                    operation.UserId = random.Next(1, CurrentUserId + 1);
                    operation.TopN = random.Next(1, 100);
                    operation.AroundN = random.Next(1, 10);
                }
                else if (operationType < 0.9) // 20% AddOrUpdateUser
                {
                    operation.Type = OperationType.AddOrUpdateUser;
                    double userType = random.NextDouble();
                    if (userType < 0.2) // 20% 新用户
                    {
                        operation.UserId = CurrentUserId++;
                    }
                    else // 80% 更新现有用户
                    {
                        operation.UserId = random.Next(1, CurrentUserId + 1);
                    }

                    if (UserIdToScore.TryGetValue(operation.UserId, out int score))
                    {
                        operation.Score = score + GeneratePowerLawScore(random, 100);
                    }
                    else
                    {
                        operation.Score = GeneratePowerLawScore(random, 100);
                    }
                }
                else // 10% GetUserRank
                {
                    operation.Type = OperationType.GetUserRank;
                    operation.UserId = random.Next(1, CurrentUserId + 1);
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
                    ID = i + 1,
                    Score = GeneratePowerLawScore(random),
                    LastActive = DateTime.Now
                };
                UserIdToScore[users[i].ID] = users[i].Score;
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

            var initialUsersJson = File.ReadAllText(InitialUsersFilePath);
            var initialUsers = JsonSerializer.Deserialize<User[]>(initialUsersJson) ??
                               throw new Exception("无法加载初始用户数据");

            // 创建排行榜实例
            var rankingList = DllMain.CreateRankingList(initialUsers, rankingListName);

            // 开始内存监控
            _peakMemoryUsage = 0;
            var memoryMonitorThread = new Thread(MonitorMemoryUsage);
            memoryMonitorThread.IsBackground = true;
            memoryMonitorThread.Start();

            // 开始测试计时
            var stopwatch = Stopwatch.StartNew();

            // 收集操作结果
            var operationResults = new List<OperationResult>();

            // 顺序执行所有操作
            foreach (var operation in operations)
            {
                var opResult = new OperationResult
                {
                    Type = operation.Type,
                    UserId = operation.UserId
                };

                switch (operation.Type)
                {
                    case OperationType.GetRankingList:
                        var listResult =
                            rankingList.GetRankingListMutiResponse(operation.TopN, operation.UserId, operation.AroundN);
                        opResult.RankingListResult = listResult;
                        break;
                    case OperationType.AddOrUpdateUser:
                        rankingList.AddOrUpdateUser(operation.UserId, operation.Score, DateTime.Now);
                        break;
                    case OperationType.GetUserRank:
                        var rankResult = rankingList.GetUserRank(operation.UserId);
                        opResult.UserRankResult = rankResult;
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

            // 如果是基准测试，保存操作结果
            if (isBenchmark)
            {
                var benchmarkResults = new BenchmarkResults
                {
                    Results = operationResults,
                    PerformanceResult = result
                };
                SaveBaseOperationResults(benchmarkResults);
            }
            else
            {
                // 验证测试结果与基准
                ValidateResults(operationResults);
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
            if (!File.Exists(BaseResultFilePath))
            {
                throw new Exception("基准结果文件不存在，请先运行 --init 命令");
            }

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
            var baseResults = LoadBaseOperationResults().Results;
            if (testResults.Count != baseResults.Count)
            {
                throw new Exception($"操作结果数量不匹配: 测试结果 {testResults.Count} 个，基准结果 {baseResults.Count} 个");
            }

            Console.WriteLine($"\n=== 验证操作结果与基准对比 ===");
            int matchedCount = 0;
            int mismatchedCount = 0;

            for (int i = 0; i < testResults.Count; i++)
            {
                var testResult = testResults[i];
                var baseResult = baseResults[i];

                if (CompareOperationResults(testResult, baseResult))
                {
                    matchedCount++;
                }
                else
                {
                    mismatchedCount++;
                    Console.WriteLine($"操作 {i + 1} 结果不匹配:");
                    Console.WriteLine($"  操作类型: {testResult.Type}");
                    Console.WriteLine($"  用户ID: {testResult.UserId}");

                    if (testResult.Type == OperationType.GetUserRank)
                    {
                        Console.WriteLine(
                            $"  测试结果: Rank={testResult.UserRankResult?.Rank}, Score={testResult.UserRankResult?.User?.Score}");
                        Console.WriteLine(
                            $"  基准结果: Rank={baseResult.UserRankResult?.Rank}, Score={baseResult.UserRankResult?.User?.Score}");
                    }
                    else if (testResult.Type == OperationType.GetRankingList)
                    {
                        Console.WriteLine($"  测试结果: TotalUsers={testResult.RankingListResult?.TotalUsers}");
                        Console.WriteLine($"  基准结果: TotalUsers={baseResult.RankingListResult?.TotalUsers}");
                    }
                }
            }

            Console.WriteLine($"\n=== 验证结果总结 ===");
            Console.WriteLine($"总操作数: {testResults.Count}");
            Console.WriteLine($"匹配数: {matchedCount}");
            Console.WriteLine($"不匹配数: {mismatchedCount}");

            if (mismatchedCount > 0)
            {
                Console.WriteLine($"测试失败，发现 {mismatchedCount} 个不匹配的操作结果");
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

            if (testResult.UserId != baseResult.UserId)
                return false;

            switch (testResult.Type)
            {
                case OperationType.GetUserRank:
                    return CompareUserRankResults(testResult.UserRankResult, baseResult.UserRankResult);
                case OperationType.GetRankingList:
                    return CompareRankingListResults(testResult.RankingListResult, baseResult.RankingListResult);
                case OperationType.AddOrUpdateUser:
                    return true; // AddOrUpdateUser 没有返回结果需要验证
                default:
                    return true;
            }
        }

        // 对比用户排名结果
        private bool CompareUserRankResults(RankingListSingleResponse? testResult,
            RankingListSingleResponse? baseResult)
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
            if (testResult.User.ID != baseResult.User.ID)
                return false;
            if (testResult.User.Score != baseResult.User.Score)
                return false;
            return true;
        }

        // 对比排行榜结果
        private bool CompareRankingListResults(RankingListMutiResponse? testResult, RankingListMutiResponse? baseResult)
        {
            if (testResult == null && baseResult == null)
                return true;
            if (testResult == null || baseResult == null)
                return false;
            if (testResult.TotalUsers != baseResult.TotalUsers)
                return false;

            // 对比 TopNUsers
            if (!CompareUserArrayResults(testResult.TopNUsers, baseResult.TopNUsers))
                return false;

            // 对比 RankingAroundUsers
            if (!CompareUserArrayResults(testResult.RankingAroundUsers, baseResult.RankingAroundUsers))
                return false;

            return true;
        }

        // 对比用户数组结果
        private bool CompareUserArrayResults(RankingListSingleResponse[]? testResults,
            RankingListSingleResponse[]? baseResults)
        {
            if (testResults == null && baseResults == null)
                return true;
            if (testResults == null || baseResults == null)
                return false;
            if (testResults.Length != baseResults.Length)
                return false;

            for (int i = 0; i < testResults.Length; i++)
            {
                if (!CompareUserRankResults(testResults[i], baseResults[i]))
                    return false;
            }

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
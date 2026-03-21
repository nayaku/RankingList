using RankingListNew;

namespace RankingListTestNew
{
    // 测试初始类
    public class TestData
    {
        public List<User> Users { get; set; }
        public List<TestOperation> Operations { get; set; }
        public TestOperationType? LimitOperationType { get; set; }
    }

    /// <summary>
    /// 操作类型枚举
    /// </summary>
    public enum TestOperationType : byte
    {
        AddUser,
        UpdateUser,
        GetUserRank,
        GetTopN,
        GetAroundUser,
    }

    /// <summary>
    /// 测试操作类
    /// </summary>
    public struct TestOperation
    {
        public int Id;
        public TestOperationType Type;
        public int UserId;
        public int ScoreOrN;
    }

    /// <summary>
    /// 测试结果类
    /// </summary>
    public struct OperationResult
    {
        public int Id;
        public TestOperationType OperationType;
        public int Rank;
        public User[]? Users;
    }

    /// <summary>
    /// 测试性能结果类
    /// </summary>
    public class TestResult
    {
        public string TestName { get; set; }
        public string RankingListName { get; set; }
        public int InitUserNum { get; set; }
        public TestOperationType? LimitOperationType { get; set; }
        public int OperationNum { get; set; }
        public int RankingListUserNum { get; set; }
        public long TotalTimeMs { get; set; }
        public double AverageTimeMs { get; set; }
        public long MemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
        public long RankingListMemoryUsage { get; set; }
        public DateTime TestDate { get; set; }
    }


}

using RankingListNew;

namespace RankingListTestNew
{
    /// <summary>
    /// 操作类型枚举
    /// </summary>
    public enum TestOperationType
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
        public List<User>? Users;
    }

    /// <summary>
    /// 测试性能结果类
    /// </summary>
    public class TestResult
    {
        public string RankingListName { get; set; }
        public long TotalTimeMs { get; set; }
        public double AverageTimeMs { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long PeakMemoryUsageBytes { get; set; }
        public DateTime TestDate { get; set; }
    }


}

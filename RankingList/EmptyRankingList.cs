using RankingList;

public class EmptyRankingList : IRankingList
{
    public void AddOrUpdateUser(int userId, int score, DateTime lastActive)
    {
        // No operation
    }

    public RankingListMutiResponse GetRankingListMutiResponse(int topN, int aroundUserId, int aroundN)
    {
        return new RankingListMutiResponse
        {
            TopNUsers = [],
            RankingAroundUsers = [],
            TotalUsers = 0
        };
    }

    public RankingListSingleResponse GetUserRank(int userId)
    {
        return new RankingListSingleResponse {
            User = new User
            {
                ID = userId,
                Score = 0,
                LastActive = DateTime.MinValue
            }
        };
    }
}
/*
=== 测试结果 ===
排行榜名称: EmptyRankingList
总耗时: 0 ms
平均耗时: 0.00 ms/操作
内存占用: 463.20 MB
内存峰值: 463.20 MB
测试日期: 2026/1/7 15:59:18

=== 与基准 SimpleRankingList 的对比 ===
总耗时: 0 ms vs 30776 ms (-100.00%)
平均耗时: 0.00 ms vs 30.78 ms (-100.00%)
内存占用: 463.20 MB vs 426.91 MB (+8.50%)
内存峰值: 463.20 MB vs 487.13 MB (-4.91%)
*/

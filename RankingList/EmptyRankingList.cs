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
内存占用: 471.23 MB
内存峰值: 470.58 MB
测试日期: 2026/1/7 16:10:00

=== 与基准 SimpleRankingList 的对比 ===
总耗时: 0 ms vs 27917 ms (-100.00%)
平均耗时: 0.00 ms vs 27.92 ms (-100.00%)
内存占用: 471.23 MB vs 425.84 MB (+10.66%)
内存峰值: 470.58 MB vs 487.53 MB (-3.48%)
*/

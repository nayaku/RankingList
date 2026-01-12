namespace RankingList
{
    public class EmptyRankingList : IRankingList
    {
        public RankingListResponse AddUser(IUser user)
        {
            return new RankingListResponse
            {
                User = user,
                Rank = 1
            };
        }

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            return [];
        }

        public int GetRankingCount()
        {
            return 0;
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            return [];
        }

        public RankingListResponse GetUserRank(int userId)
        {
            return new RankingListResponse
            {
                User = new User()
                {
                    Id = userId,
                },
                Rank = 0
            };
        }

        public RankingListResponse UpdateUser(IUser user)
        {
            return new RankingListResponse
            {
                User = user,
                Rank = 0
            };
        }
    }
}
/*
=== 测试结果 ===
排行榜名称: EmptyRankingList
总耗时: 11 ms
平均耗时: 0.00 ms/操作
内存占用: 474.46 MB
内存峰值: 473.83 MB
测试日期: 2026/1/12 15:05:11

=== 与基准 SimpleRankingList 的对比 ===
总耗时: 11 ms vs 367868 ms (-100.00%)
平均耗时: 0.00 ms vs 36.79 ms (-100.00%)
内存占用: 474.46 MB vs 427.65 MB (+10.94%)
内存峰值: 473.83 MB vs 454.34 MB (+4.29%)
*/
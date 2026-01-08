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
总耗时: 6 ms
平均耗时: 0.00 ms/操作
内存占用: 4936.74 MB
内存峰值: 4937.54 MB
测试日期: 2026/1/8 17:26:48

=== 与基准 SimpleRankingList2 的对比 ===
总耗时: 6 ms vs 8884 ms (-99.93%)
平均耗时: 0.00 ms vs 0.89 ms (-99.93%)
内存占用: 4936.74 MB vs 4737.48 MB (+4.21%)
内存峰值: 4937.54 MB vs 4737.48 MB (+4.22%)
*/
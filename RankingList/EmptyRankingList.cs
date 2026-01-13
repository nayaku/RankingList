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
总耗时: 23 ms
平均耗时: 0.00 ms/操作
内存占用: 137.27 MB
内存峰值: 136.82 MB
测试日期: 2026/1/12 17:52:34

=== 与基准 SimpleRankingList 的对比 ===
总耗时: 23 ms vs 59806 ms (-99.96%)
平均耗时: 0.00 ms vs 0.30 ms (-99.96%)
内存占用: 137.27 MB vs 185.58 MB (-26.03%)
内存峰值: 136.82 MB vs 213.66 MB (-35.96%)
*/
namespace RankingList
{
    internal class SimpleRankingList : IRankingList
    {
        public List<IUser> Users { get; set; }

        public SimpleRankingList(IUser[] users) 
        {
            Users = [.. users];
            Users.Sort();
        }
      
        public RankingListResponse AddUser(IUser user)
        {
            Users.Add(user);
            Users.Sort();
            return new RankingListResponse
            {
                User = user,
                Rank = Users.IndexOf(user) + 1
            };
        }

        public RankingListResponse UpdateUser(IUser user)
        {
            var existingUser = Users.FirstOrDefault(u => u.Id == user.Id);
            if (existingUser == null)
            {
                throw new ArgumentException($"用户 {user.Id} 不存在");
            }
            Users.Remove(existingUser);
            Users.Add(user);
            Users.Sort();
            return new RankingListResponse
            {
                User = user,
                Rank = Users.IndexOf(user) + 1
            };
        }

        RankingListResponse IRankingList.GetUserRank(int userId)
        {
            var index = Users.FindIndex(u => u.Id == userId);
            if (index == -1) return null;
            return new RankingListResponse
            {
                User = Users[index],
                Rank = index + 1
            };
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            var count = Math.Min(topN, Users.Count);
            var result = new RankingListResponse[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new RankingListResponse
                {
                    User = Users[i],
                    Rank = i + 1
                };
            }
            return result;
        }

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            var index = Users.FindIndex(u => u.Id == userId);
            if (index == -1) return [];
            int start = Math.Max(0, index - aroundN);
            int end = Math.Min(Users.Count - 1, index + aroundN);
            int count = end - start + 1;
            var result = new RankingListResponse[count];
            for (int i = start; i <= end; i++)
            {
                result[i - start] = new RankingListResponse
                {
                    User = Users[i],
                    Rank = i + 1
                };
            }
            return result;
        }

        public int GetRankingCount()
        {
            return Users.Count;
        }
    }
}
/*
=== 生成基准数据 ===
运行基准测试（SimpleRankingList）...
基准操作结果已保存到 base_operation_results.json
基准结果已保存到 base_result.json

=== 基准测试结果 ===
排行榜名称: SimpleRankingList
总耗时: 367868 ms
平均耗时: 36.79 ms/操作
内存占用: 427.65 MB
内存峰值: 454.34 MB
测试日期: 2026/1/12 15:02:12
*/
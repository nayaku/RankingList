namespace RankingList
{
    /// <summary>
    /// 简单排名列表实现 2
    /// <note>
    /// 优化了用户更新的性能，使用二分查找插入用户，避免每次更新后排序整个列表。
    /// </note>
    /// </summary>
    internal class SimpleRankingList2 : IRankingList
    {
        private readonly List<IUser> _users;
        private readonly Dictionary<int, int> _userId2Index;

        public SimpleRankingList2(IUser[] users)
        {
            _users = [.. users];
            _users.Sort();
            _userId2Index = _users.Select((user, i) => new { user.Id, i })
                .ToDictionary(x => x.Id, x => x.i);
        }

        public RankingListResponse AddUser(IUser user)
        {
            var insertIndex = _users.BinarySearch(user);
            if (insertIndex < 0)
            {
                insertIndex = ~insertIndex;
            }

            _users.Insert(insertIndex, user);

            return new RankingListResponse
            {
                User = user,
                Rank = insertIndex + 1
            };
        }

        public RankingListResponse UpdateUser(IUser user)
        {
            // 移除旧用户
            if (_userId2Index.TryGetValue(user.Id, out var existingUserIndex))
            {
                _users.RemoveAt(existingUserIndex);
            }

            // 插入新用户
            var insertIndex = _users.BinarySearch(user);
            if (insertIndex < 0)
            {
                insertIndex = ~insertIndex;
            }

            _users.Insert(insertIndex, user);
            _userId2Index[user.Id] = insertIndex;

            return new RankingListResponse
            {
                User = user,
                Rank = insertIndex + 1
            };
        }

        public RankingListResponse GetUserRank(int userId)
        {
            if (!_userId2Index.TryGetValue(userId, out int index))
            {
                return new RankingListResponse
                {
                    User = null,
                    Rank = -1
                };
            }

            return new RankingListResponse
            {
                User = _users[index],
                Rank = index + 1
            };
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            var count = Math.Min(topN, _users.Count);
            var result = new RankingListResponse[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = new RankingListResponse
                {
                    User = _users[i],
                    Rank = i + 1
                };
            }

            return result;
        }

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            if (!_userId2Index.TryGetValue(userId, out int index))
            {
                return [];
            }

            int start = Math.Max(0, index - aroundN);
            int end = Math.Min(_users.Count - 1, index + aroundN);
            int count = end - start + 1;

            var result = new RankingListResponse[count];
            for (int i = start; i <= end; i++)
            {
                result[i - start] = new RankingListResponse
                {
                    User = _users[i],
                    Rank = i + 1
                };
            }

            return result;
        }

        public int GetRankingCount()
        {
            return _users.Count;
        }
    }
}
/*
=== 基准测试结果 ===
排行榜名称: SimpleRankingList2
总耗时: 8884 ms
平均耗时: 0.89 ms/操作
内存占用: 4737.48 MB
内存峰值: 4737.48 MB
测试日期: 2026/1/8 17:24:32
*/
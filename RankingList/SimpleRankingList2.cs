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
        private List<User> _users;
        private Dictionary<int, User> _userDictionary;
        private readonly object _lock = new();

        public SimpleRankingList2(User[] users)
        {
            _users = [.. users];
            _userDictionary = users.ToDictionary(u => u.ID);
            _users.Sort();
        }
        public void AddOrUpdateUser(User user)
        {
            lock (_lock)
            {
                if (_userDictionary.TryGetValue(user.ID, out var existingUser))
                {
                    _users.Remove(existingUser);
                    _userDictionary.Remove(user.ID);
                }
                var index = _users.BinarySearch(user);
                if (index < 0)
                {
                    index = ~index;
                }
                _users.Insert(index, user);
                _userDictionary[user.ID] = user;
            }
        }
        public void AddOrUpdateUser(int userId, int score, DateTime lastActive)
        {
            var user = new User
            {
                ID = userId,
                Score = score,
                LastActive = lastActive
            };
            AddOrUpdateUser(user);
        }
        public RankingListSingleResponse GetUserRank(int userId)
        {
            lock (_lock)
            {
                if (!_userDictionary.ContainsKey(userId))
                {
                    return null;
                }
                var user = _userDictionary[userId];
                var index = _users.IndexOf(user);
                if (index == -1) return null;
                return new RankingListSingleResponse
                {
                    User = _users[index],
                    Rank = index + 1
                };
            }
        }
        public RankingListMutiResponse GetRankingListMutiResponse(int topN, int aroundUserId, int aroundN)
        {
            lock (_lock)
            {
                var response = new RankingListMutiResponse
                {
                    TopNUsers = [],
                    RankingAroundUsers = [],
                    TotalUsers = _users.Count
                };
                // Top N users
                var topNNum = Math.Min(topN, _users.Count);
                response.TopNUsers = new RankingListSingleResponse[topNNum];
                for (int i = 0; i < topNNum; i++)
                {
                    response.TopNUsers[i] = new RankingListSingleResponse
                    {
                        User = _users[i],
                        Rank = i + 1
                    };
                }
                // Users around a specific user
                if (!_userDictionary.TryGetValue(aroundUserId, out User? user))
                {
                    return response;
                }
                var index = _users.IndexOf(user);
                if (index != -1)
                {
                    int start = Math.Max(0, index - aroundN);
                    int end = Math.Min(_users.Count - 1, index + aroundN);
                    int count = end - start + 1;
                    response.RankingAroundUsers = new RankingListSingleResponse[count];
                    for (int i = start; i <= end; i++)
                    {
                        response.RankingAroundUsers[i - start] = new RankingListSingleResponse
                        {
                            User = _users[i],
                            Rank = i + 1
                        };
                    }
                }
                return response;
            }
        }
    }
}
/*
=== 测试结果 ===
排行榜名称: SimpleRankingList2
总耗时: 3873 ms
平均耗时: 3.87 ms/操作
内存占用: 453.57 MB
内存峰值: 497.61 MB
测试日期: 2026/1/7 16:10:57

=== 与基准 SimpleRankingList 的对比 ===
总耗时: 3873 ms vs 27917 ms (-86.13%)
平均耗时: 3.87 ms vs 27.92 ms (-86.13%)
内存占用: 453.57 MB vs 425.84 MB (+6.51%)
内存峰值: 497.61 MB vs 487.53 MB (+2.07%)
*/
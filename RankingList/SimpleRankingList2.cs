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

        public SimpleRankingList2(List<User> users)
        {
            _users = users;
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
                for (int i = 0; i < Math.Min(topN, _users.Count); i++)
                {
                    response.TopNUsers.Add(new RankingListSingleResponse
                    {
                        User = _users[i],
                        Rank = i + 1
                    });
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
                    for (int i = start; i <= end; i++)
                    {
                        response.RankingAroundUsers.Add(new RankingListSingleResponse
                        {
                            User = _users[i],
                            Rank = i + 1
                        });
                    }
                }
                return response;
            }
        }
    }
}
/*
2026年1月6日18:29:15
=== Ranking List Concurrent Test ===

Starting ranking list server from: RankingListServer.exe
Server started with PID: 23744
=== Ranking List Server ===
Starting server...
Ranking list initialized.
Named pipe server started. Pipe name: RankingListPipe
Press any key to stop the server...
Starting concurrent test...
Initial users: 1000000
Total operations: 1000
Concurrency level: 100

Test Results:
Total time elapsed: 3354 ms
Completed operations: 1000
Maximum concurrent operations: 20
Average response time: 63.01 ms
Throughput: 298.13 operations/second
Peak memory usage: 113.01 MB
Server process with PID 23744 stopped.
*/
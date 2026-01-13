using System.Diagnostics;

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
            Debug.Assert(!_userId2Index.ContainsKey(user.Id), "用户已存在");
            var insertIndex = _users.BinarySearch(user);
            if (insertIndex < 0)
            {
                insertIndex = ~insertIndex;
            }

            _users.Insert(insertIndex, user);
            for (int i = insertIndex; i < _users.Count; i++)
            {
                _userId2Index[_users[i].Id] = i;
            }

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
            else
            {
                Debug.Assert(false, "用户不存在");
            }

            // 插入新用户
            var insertIndex = _users.BinarySearch(user);
            if (insertIndex < 0)
            {
                insertIndex = ~insertIndex;
            }

            _users.Insert(insertIndex, user);
            int minIndex = Math.Min(insertIndex, existingUserIndex);
            for (int i = minIndex; i < _users.Count; i++)
            {
                _userId2Index[_users[i].Id] = i;
            }

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
=== 排行榜测试框架 ===

=== 生成基准数据 ===
运行基准测试（SimpleRankingList2）...
总操作数: 300000
初始用户数: 10000
基准操作结果已保存到 base_operation_results.json
基准结果已保存到 base_result.json

=== 基准测试结果 ===
排行榜名称: SimpleRankingList2
总耗时: 8517 ms
平均耗时: 0.03 ms/操作
内存占用: 259.20 MB
内存峰值: 288.79 MB
测试日期: 2026/1/12 18:20:54
*/
using System.Diagnostics;

namespace RankingListNew
{
    public class BListRankingList : IRankingList
    {
        private readonly List<User> _users;
        private readonly Dictionary<int, User> _usersDict;

        public BListRankingList(List<User> users)
        {
            _users = [.. users];
            _users.Sort();
            _usersDict = _users.ToDictionary(u => u.Id);
        }

        public int AddUser(User user)
        {
            int insertIndex = _users.BinarySearch(user);
            if (insertIndex < 0)
            {
                insertIndex = ~insertIndex;
            }

            _users.Insert(insertIndex, user);
            _usersDict[user.Id] = user;
            return insertIndex;
        }

        public int UpdateUser(User user)
        {
            // 移除旧用户
            int oldIndex = GetUserRank(user.Id);
            _users.RemoveAt(oldIndex);
            // 插入新用户
            int insertIndex = AddUser(user);
            return insertIndex;
        }

        public int GetUserRank(int userId)
        {
            User user = _usersDict[userId];
            int index = _users.BinarySearch(user);
            Debug.Assert(index >= 0);
            Debug.Assert(_users[index].Id == userId);
            return index;
        }

        public List<User> GetTopN(int topN)
        {
            int count = Math.Min(topN, _users.Count);
            List<User> result = _users.GetRange(0, count);

            return result;
        }

        public (List<User>, int) GetAroundUser(int userId, int aroundN)
        {
            int index = GetUserRank(userId);
            int start = Math.Max(0, index - aroundN);
            int end = Math.Min(_users.Count - 1, index + aroundN);
            int count = end - start + 1;
            List<User> result = _users.GetRange(start, count);
            return (result, index);
        }

        public int GetRankingCount()
        {
            return _users.Count;
        }

        public void DebugPrint()
        {
            Console.WriteLine($"最终用户数: {_users.Count} users.");
        }
    }
}
/*
== Test t0_100 ===
用户数: 0
操作数: 83
总耗时: 2 ms
平均耗时: 24.10 ms/1000操作
内存占用: 0.01 MB
内存峰值: 0.02 MB
测试日期: 2026/2/17 20:34:36
√ 所有操作结果验证通过！
总耗时: 2 ms vs 2 ms (0.00%)
平均耗时: 24.10 ms/k操作 vs 24.10 ms/1k操作 (0.00%)
内存占用: 0.01 MB vs 0.01 MB (+5.00%)
内存峰值: 0.02 MB vs 0.03 MB (-25.72%)
== Test t0_100 End ===

== Test t0_1w ===
用户数: 0
操作数: 9983
总耗时: 4 ms
平均耗时: 0.40 ms/1000操作
内存占用: 4.08 MB
内存峰值: 4.21 MB
测试日期: 2026/2/17 20:34:36
√ 所有操作结果验证通过！
总耗时: 4 ms vs 157 ms (-97.45%)
平均耗时: 0.40 ms/k操作 vs 15.73 ms/1k操作 (-97.45%)
内存占用: 4.08 MB vs 4.03 MB (+1.36%)
内存峰值: 4.21 MB vs 4.51 MB (-6.56%)
== Test t0_1w End ===

== Test t100_1w ===
用户数: 100
操作数: 10000
总耗时: 10 ms
平均耗时: 1.00 ms/1000操作
内存占用: 4.16 MB
内存峰值: 4.28 MB
测试日期: 2026/2/17 20:34:37
√ 所有操作结果验证通过！
总耗时: 10 ms vs 171 ms (-94.15%)
平均耗时: 1.00 ms/k操作 vs 17.10 ms/1k操作 (-94.15%)
内存占用: 4.16 MB vs 4.09 MB (+1.58%)
内存峰值: 4.28 MB vs 4.58 MB (-6.41%)
== Test t100_1w End ===

== Test t1k_10w ===
用户数: 1000
操作数: 100000
总耗时: 64 ms
平均耗时: 0.64 ms/1000操作
内存占用: 41.54 MB
内存峰值: 42.21 MB
测试日期: 2026/2/17 20:34:37
√ 所有操作结果验证通过！
总耗时: 64 ms vs 2983 ms (-97.85%)
平均耗时: 0.64 ms/k操作 vs 29.83 ms/1k操作 (-97.85%)
内存占用: 41.54 MB vs 40.76 MB (+1.90%)
内存峰值: 42.21 MB vs 41.61 MB (+1.46%)
== Test t1k_10w End ===
*/
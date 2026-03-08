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

        public User[] GetTopN(int topN)
        {
            int count = Math.Min(topN, _users.Count);
            User[] result = [.. _users.GetRange(0, count)];
            return result;
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            int rank = GetUserRank(userId);
            int start = Math.Max(0, rank - aroundN);
            int end = Math.Min(_users.Count - 1, rank + aroundN);
            int count = end - start + 1;
            User[] result = [.. _users.GetRange(start, count)];
            return (result, rank);
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
测试类: BListRankingList
基准测试类: BucketBRTreeRankingList
== Test stau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: AddUser
排行榜用户数: 200000
总耗时: 371 ms
平均耗时: 3.71 ms/1000操作
内存占用: 7.82 MB
内存峰值: 13.09 MB
测试日期: 2026/3/8 22:26:55
√ 所有操作结果验证通过！
总耗时: 371 ms vs 23 ms (+1513.04%)
平均耗时: 3.71 ms/1k操作 vs 0.23 ms/1k操作 (+1513.04%)
内存占用: 7.82 MB vs 9.30 MB (-15.90%)
内存峰值: 13.09 MB vs 13.05 MB (+0.33%)
== Test stau10w_10w End ===

== Test stgau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetAroundUser
排行榜用户数: 100000
总耗时: 76 ms
平均耗时: 0.76 ms/1000操作
内存占用: 36.67 MB
内存峰值: 40.89 MB
测试日期: 2026/3/8 22:26:55
√ 所有操作结果验证通过！
总耗时: 76 ms vs 52 ms (+46.15%)
平均耗时: 0.76 ms/1k操作 vs 0.52 ms/1k操作 (+46.15%)
内存占用: 36.67 MB vs 36.66 MB (+0.02%)
内存峰值: 40.89 MB vs 36.68 MB (+11.48%)
== Test stgau10w_10w End ===

== Test stgt10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetTopN
排行榜用户数: 100000
总耗时: 48 ms
平均耗时: 0.48 ms/1000操作
内存占用: 80.91 MB
内存峰值: 81.24 MB
测试日期: 2026/3/8 22:26:57
√ 所有操作结果验证通过！
总耗时: 48 ms vs 26 ms (+84.62%)
平均耗时: 0.48 ms/1k操作 vs 0.26 ms/1k操作 (+84.62%)
内存占用: 80.91 MB vs 80.88 MB (+0.05%)
内存峰值: 81.24 MB vs 80.96 MB (+0.34%)
== Test stgt10w_10w End ===

== Test stgu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetUserRank
排行榜用户数: 100000
总耗时: 18 ms
平均耗时: 0.18 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:27:00
√ 所有操作结果验证通过！
总耗时: 18 ms vs 27 ms (-33.33%)
平均耗时: 0.18 ms/1k操作 vs 0.27 ms/1k操作 (-33.33%)
内存占用: 2.29 MB vs 2.29 MB (+0.02%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stgu10w_10w End ===

== Test stuu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: UpdateUser
排行榜用户数: 100000
总耗时: 1356 ms
平均耗时: 13.56 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:27:01
√ 所有操作结果验证通过！
总耗时: 1356 ms vs 59 ms (+2198.31%)
平均耗时: 13.56 ms/1k操作 vs 0.59 ms/1k操作 (+2198.31%)
内存占用: 2.29 MB vs 2.29 MB (0.00%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stuu10w_10w End ===

== Test t100w_100w ===
用户数: 1000000
操作数: 1000000
排行榜用户数: 1099921
总耗时: 91392 ms
平均耗时: 91.39 ms/1000操作
内存占用: 263.86 MB
内存峰值: 269.32 MB
测试日期: 2026/3/8 22:28:34
√ 所有操作结果验证通过！
总耗时: 91392 ms vs 544 ms (+16700.00%)
平均耗时: 91.39 ms/1k操作 vs 0.54 ms/1k操作 (+16700.00%)
内存占用: 263.86 MB vs 251.84 MB (+4.77%)
内存峰值: 269.32 MB vs 251.83 MB (+6.94%)
== Test t100w_100w End ===

== Test t10w_10w ===
用户数: 100000
操作数: 100000
排行榜用户数: 109905
总耗时: 419 ms
平均耗时: 4.19 ms/1000操作
内存占用: 30.37 MB
内存峰值: 42.27 MB
测试日期: 2026/3/8 22:28:42
√ 所有操作结果验证通过！
总耗时: 419 ms vs 23 ms (+1721.74%)
平均耗时: 4.19 ms/1k操作 vs 0.23 ms/1k操作 (+1721.74%)
内存占用: 30.37 MB vs 29.07 MB (+4.47%)
内存峰值: 42.27 MB vs 32.83 MB (+28.76%)
== Test t10w_10w End ===
*/

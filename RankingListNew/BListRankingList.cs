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
 == Test stau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: AddUser
排行榜用户数: 200000
总耗时: 381 ms
平均耗时: 3.81 ms/1000操作
内存占用: 7.82 MB
内存峰值: 13.09 MB
测试日期: 2026/3/8 20:33:53
√ 所有操作结果验证通过！
总耗时: 381 ms vs 238 ms (+60.08%)
平均耗时: 3.81 ms/1k操作 vs 2.38 ms/1k操作 (+60.08%)
内存占用: 7.82 MB vs 9.18 MB (-14.84%)
内存峰值: 13.09 MB vs 12.94 MB (+1.15%)
== Test stau10w_10w End ===

== Test stgau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetAroundUser
排行榜用户数: 100000
总耗时: 62 ms
平均耗时: 0.62 ms/1000操作
内存占用: 36.67 MB
内存峰值: 40.89 MB
测试日期: 2026/3/8 20:33:53
√ 所有操作结果验证通过！
总耗时: 62 ms vs 357 ms (-82.63%)
平均耗时: 0.62 ms/1k操作 vs 3.57 ms/1k操作 (-82.63%)
内存占用: 36.67 MB vs 36.67 MB (0.00%)
内存峰值: 40.89 MB vs 39.16 MB (+4.39%)
== Test stgau10w_10w End ===

== Test stgt10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetTopN
排行榜用户数: 100000
总耗时: 46 ms
平均耗时: 0.46 ms/1000操作
内存占用: 80.91 MB
内存峰值: 81.97 MB
测试日期: 2026/3/8 20:33:55
√ 所有操作结果验证通过！
总耗时: 46 ms vs 23 ms (+100.00%)
平均耗时: 0.46 ms/1k操作 vs 0.23 ms/1k操作 (+100.00%)
内存占用: 80.91 MB vs 80.88 MB (+0.05%)
内存峰值: 81.97 MB vs 80.97 MB (+1.23%)
== Test stgt10w_10w End ===

== Test stgu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetUserRank
排行榜用户数: 100000
总耗时: 19 ms
平均耗时: 0.19 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 20:33:58
√ 所有操作结果验证通过！
总耗时: 19 ms vs 190 ms (-90.00%)
平均耗时: 0.19 ms/1k操作 vs 1.90 ms/1k操作 (-90.00%)
内存占用: 2.29 MB vs 2.29 MB (0.00%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stgu10w_10w End ===

== Test stuu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: UpdateUser
排行榜用户数: 100000
总耗时: 1389 ms
平均耗时: 13.89 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 20:33:59
√ 所有操作结果验证通过！
总耗时: 1389 ms vs 244 ms (+469.26%)
平均耗时: 13.89 ms/1k操作 vs 2.44 ms/1k操作 (+469.26%)
内存占用: 2.29 MB vs 2.29 MB (0.00%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stuu10w_10w End ===
*/

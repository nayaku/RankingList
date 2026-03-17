using System.Diagnostics;

namespace RankingListNew
{
    public class BucketListRankingList : IRankingList
    {
        private int _userCount;
        private List<UserBucket> _buckets;
        private Dictionary<int, User> _userDict;
#if DEBUG
        private int _splitCount;
#endif
        public BucketListRankingList(List<User> users)
        {
            users.Sort();
            int bucketNum = (int)Math.Ceiling((double)users.Count / UserBucket.InitialBucketSize);
            // 初始化每个桶
            _buckets = new(bucketNum);
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * UserBucket.InitialBucketSize;
                int r = Math.Min((i + 1) * UserBucket.InitialBucketSize, users.Count);
                int userCount = r - l;
                User[] bucketUsers = new User[UserBucket.BucketSize];
                users.CopyTo(l, bucketUsers, 0, userCount);
                _buckets.Add(new UserBucket(bucketUsers, userCount));
            }
            if (users.Count == 0)
            {
                User[] bucketUsers = new User[UserBucket.BucketSize];
                _buckets.Add(new UserBucket(bucketUsers, 0));
            }
            _userDict = users.ToDictionary(u => u.Id, u => u);
            _userCount = users.Count;
        }

        public int AddUser(User user)
        {
            int rankCount = 0;
            if (_userCount == 0)
            {
                _buckets[0].Insert(user);
            }
            else
            {
                int bucketIndex;
                int userIndexInBucket;
                for (bucketIndex = 0; bucketIndex < _buckets.Count - 1; bucketIndex++)
                // 找不到就选择最后一个bucket
                {
                    if (user.CompareTo(_buckets[bucketIndex].MaxUser) <= 0)
                    {
                        break;
                    }
                    rankCount += _buckets[bucketIndex].UserCount;
                }

                if (_buckets[bucketIndex].Full)
                {
                    // 分裂bucket
                    UserBucket newBucket = _buckets[bucketIndex].Split(user, out userIndexInBucket);
                    _buckets.Insert(bucketIndex + 1, newBucket);
#if DEBUG
                    _splitCount++;
#endif
                }
                else
                {
                    // 加入bucket
                    userIndexInBucket = _buckets[bucketIndex].Insert(user);
                }
                rankCount += userIndexInBucket;
            }
            _userDict[user.Id] = user;
            _userCount++;
            return rankCount;
        }

        private void RemoveUser(User user)
        {
            int bucketIndex;
            for (bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
            {
                UserBucket bucket = _buckets[bucketIndex];
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    bucket.Remove(user);
                    break;
                }
            }

            Debug.Assert(bucketIndex < _buckets.Count, "用户不存在");
            if (_userCount == 1)
            { }
            else if (_buckets[bucketIndex].Empty)
            {
                _buckets.RemoveAt(bucketIndex);
            }

            _userCount--;
        }

        public int UpdateUser(User user)
        {
            User oldUser = _userDict[user.Id];
            RemoveUser(oldUser);
            return AddUser(user);
        }

        public int GetUserRank(int userId)
        {
            int rankCount = 0;
            User user = _userDict[userId];
            foreach (UserBucket bucket in _buckets)
            {
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    int rankInBucket = bucket.IndexOf(user);
                    Debug.Assert(rankInBucket >= 0);
                    rankCount += rankInBucket;
                    break;
                }

                rankCount += bucket.UserCount;
            }

            return rankCount;
        }

        public User[] GetTopN(int topN)
        {
            int rankCount = 0;
            User[] result = new User[Math.Min(topN, _userCount)];
            for (int bucketIndex = 0; bucketIndex < _buckets.Count && rankCount < topN; bucketIndex++)
            {
                UserBucket bucket = _buckets[bucketIndex];
                int n = Math.Min(bucket.UserCount, topN - rankCount);
                Array.Copy(bucket.Users, 0, result, rankCount, n);
                rankCount += n;
            }

            return result;
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            int rankCount = 0;
            int bucketIndex = -1;
            User user = _userDict[userId];
            for (int i = 0; i < _buckets.Count; i++)
            {
                if (user.CompareTo(_buckets[i].MaxUser) <= 0)
                {
                    bucketIndex = i;
                    break;
                }

                rankCount += _buckets[i].UserCount;
            }

            Debug.Assert(bucketIndex != -1);

            int inBucketIndex = _buckets[bucketIndex].IndexOf(user);
            Debug.Assert(inBucketIndex != -1);
            int resultRank = rankCount + inBucketIndex;
            int startRank = Math.Max(0, resultRank - aroundN);
            int endRank = Math.Min(resultRank + aroundN, _userCount - 1);
            int count = endRank - startRank + 1;

            for (; rankCount > startRank; bucketIndex--)
            {
                rankCount -= _buckets[bucketIndex - 1].UserCount;
            }

            inBucketIndex = startRank - rankCount;
            User[] result = new User[count];
            for (int resultIndex = 0; resultIndex < count; bucketIndex++)
            {
                UserBucket bucket = _buckets[bucketIndex];
                int n = Math.Min(bucket.UserCount - inBucketIndex, count - resultIndex);
                Array.Copy(bucket.Users, inBucketIndex, result, resultIndex, n);
                resultIndex += n;
                inBucketIndex = 0;
            }

            return (result, resultRank);
        }

        public int GetRankingCount()
        {
            return _userCount;
        }

#if DEBUG
        public void DebugPrint()
        {
            Console.WriteLine($"UserCount: {_userCount}");
            Console.Write("Each Bucket Number of Users: ");
            for (int i = 0; i < _buckets.Count; i++)
            {
                Console.Write($"{_buckets[i].UserCount} ");
            }

            Console.WriteLine();
            Console.WriteLine("Each Bucket Score Range:");
            for (int i = 0; i < _buckets.Count; i++)
            {
                Console.WriteLine(
                    $"Bucket {i}: {(_buckets[i].MinUser).Score} - {(_buckets[i].MaxUser).Score}");
            }

            Console.WriteLine($"SplitCount: {_splitCount}");
        }
#endif
    }
}
/*
测试类: BucketListRankingList
基准测试类: BucketBRTreeRankingList
== Test stau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: AddUser
排行榜用户数: 200000
总耗时: 239 ms
平均耗时: 2.39 ms/1000操作
内存占用: 9.18 MB
内存峰值: 12.94 MB
测试日期: 2026/3/8 22:32:43
√ 所有操作结果验证通过！
总耗时: 239 ms vs 23 ms (+939.13%)
平均耗时: 2.39 ms/1k操作 vs 0.23 ms/1k操作 (+939.13%)
内存占用: 9.18 MB vs 9.30 MB (-1.25%)
内存峰值: 12.94 MB vs 13.05 MB (-0.81%)
== Test stau10w_10w End ===

== Test stgau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetAroundUser
排行榜用户数: 100000
总耗时: 334 ms
平均耗时: 3.34 ms/1000操作
内存占用: 36.66 MB
内存峰值: 36.68 MB
测试日期: 2026/3/8 22:32:44
√ 所有操作结果验证通过！
总耗时: 334 ms vs 52 ms (+542.31%)
平均耗时: 3.34 ms/1k操作 vs 0.52 ms/1k操作 (+542.31%)
内存占用: 36.66 MB vs 36.66 MB (0.00%)
内存峰值: 36.68 MB vs 36.68 MB (0.00%)
== Test stgau10w_10w End ===

== Test stgt10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetTopN
排行榜用户数: 100000
总耗时: 21 ms
平均耗时: 0.21 ms/1000操作
内存占用: 80.88 MB
内存峰值: 80.97 MB
测试日期: 2026/3/8 22:32:46
√ 所有操作结果验证通过！
总耗时: 21 ms vs 26 ms (-19.23%)
平均耗时: 0.21 ms/1k操作 vs 0.26 ms/1k操作 (-19.23%)
内存占用: 80.88 MB vs 80.88 MB (0.00%)
内存峰值: 80.97 MB vs 80.96 MB (+0.01%)
== Test stgt10w_10w End ===

== Test stgu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetUserRank
排行榜用户数: 100000
总耗时: 192 ms
平均耗时: 1.92 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:32:48
√ 所有操作结果验证通过！
总耗时: 192 ms vs 27 ms (+611.11%)
平均耗时: 1.92 ms/1k操作 vs 0.27 ms/1k操作 (+611.11%)
内存占用: 2.29 MB vs 2.29 MB (+0.02%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stgu10w_10w End ===

== Test stuu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: UpdateUser
排行榜用户数: 100000
总耗时: 287 ms
平均耗时: 2.87 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:32:49
√ 所有操作结果验证通过！
总耗时: 287 ms vs 59 ms (+386.44%)
平均耗时: 2.87 ms/1k操作 vs 0.59 ms/1k操作 (+386.44%)
内存占用: 2.29 MB vs 2.29 MB (0.00%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stuu10w_10w End ===

== Test t100w_100w ===
用户数: 1000000
操作数: 1000000
排行榜用户数: 1099921
总耗时: 9911 ms
平均耗时: 9.91 ms/1000操作
内存占用: 251.77 MB
内存峰值: 251.81 MB
测试日期: 2026/3/8 22:32:59
√ 所有操作结果验证通过！
总耗时: 9911 ms vs 544 ms (+1721.88%)
平均耗时: 9.91 ms/1k操作 vs 0.54 ms/1k操作 (+1721.88%)
内存占用: 251.77 MB vs 251.84 MB (-0.03%)
内存峰值: 251.81 MB vs 251.83 MB (-0.01%)
== Test t100w_100w End ===

== Test t10w_10w ===
用户数: 100000
操作数: 100000
排行榜用户数: 109905
总耗时: 82 ms
平均耗时: 0.82 ms/1000操作
内存占用: 29.07 MB
内存峰值: 32.83 MB
测试日期: 2026/3/8 22:33:07
√ 所有操作结果验证通过！
总耗时: 82 ms vs 23 ms (+256.52%)
平均耗时: 0.82 ms/1k操作 vs 0.23 ms/1k操作 (+256.52%)
内存占用: 29.07 MB vs 29.07 MB (0.00%)
内存峰值: 32.83 MB vs 32.83 MB (0.00%)
== Test t10w_10w End ===

*/
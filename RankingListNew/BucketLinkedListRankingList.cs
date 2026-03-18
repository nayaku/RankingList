using System.Diagnostics;

namespace RankingListNew
{
    public class BucketLinkedListRankingList : IRankingList
    {
        private int _userCount;
        private LinkedList<UserBucket> _buckets;
        private Dictionary<int, User> _userDict;
#if DEBUG
        private int _splitCount;
        private int _combineCount;
#endif
        public BucketLinkedListRankingList(List<User> users)
        {
            users.Sort();
            int bucketNum = (int)Math.Ceiling((double)users.Count / UserBucket.InitialBucketSize);
            // 初始化每个桶
            _buckets = new();
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * UserBucket.InitialBucketSize;
                int r = Math.Min((i + 1) * UserBucket.InitialBucketSize, users.Count);
                int userCount = r - l;
                User[] bucketUsers = new User[UserBucket.BucketSize];
                users.CopyTo(l, bucketUsers, 0, userCount);
                _buckets.AddLast(new UserBucket(bucketUsers, userCount));
            }

            if (users.Count == 0)
            {
                User[] bucketUsers = new User[UserBucket.BucketSize];
                _buckets.AddLast(new UserBucket(bucketUsers, 0));
            }

            _userDict = users.ToDictionary(u => u.Id, u => u);
            _userCount = users.Count;
        }

        public int AddUser(User user)
        {
            int rankCount = 0;
            if (_userCount == 0)
            {
                _buckets.Last!.Value.Insert(user);
            }
            else
            {
                LinkedListNode<UserBucket> bucketNode = _buckets.First!;
                int userIndexInBucket;
                while (bucketNode.Next != null)
                    // 找不到就选择最后一个bucket
                {
                    if (user.CompareTo(bucketNode.Value.MaxUser) <= 0)
                    {
                        break;
                    }

                    rankCount += bucketNode.Value.UserCount;
                    bucketNode = bucketNode.Next!;
                }

                UserBucket bucket = bucketNode.Value;
                if (bucket.Full)
                {
                    UserBucket newBucket = bucket.Split(user, out userIndexInBucket);
                    // 分裂bucket
                    _buckets.AddAfter(bucketNode, newBucket);
#if DEBUG
                    _splitCount++;
#endif
                }
                else
                {
                    // 加入bucket
                    userIndexInBucket = bucket.Insert(user);
                }

                rankCount += userIndexInBucket;
            }

            _userDict[user.Id] = user;
            _userCount++;
            return rankCount;
        }

        private void RemoveUser(User user)
        {
            LinkedListNode<UserBucket> bucketNode = _buckets.First!;
            while (bucketNode != null)
            {
                UserBucket bucket = bucketNode.Value;
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    bucket.Remove(user);
                    break;
                }

                bucketNode = bucketNode.Next!;
            }

            Debug.Assert(bucketNode != null, "用户不存在");
            if (_userCount > 1)
            {
                UserBucket bucket = bucketNode.Value;
                if (bucket.Empty)
                {
                    _buckets.Remove(bucketNode);
                }
                else if (bucketNode.Previous != null
                         && bucketNode.Value.UserCount < UserBucket.CombineBucketSize
                         && bucketNode.Previous.Value.UserCount < UserBucket.CombineBucketSize)
                {
                    // 向前合并
                    bucketNode.Previous.Value.Combine(bucketNode.Value);
                    _buckets.Remove(bucketNode);
#if DEBUG
                    _combineCount++;
#endif
                }
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
            LinkedListNode<UserBucket>? bucketNode = _buckets.First;
            while (bucketNode != null)
            {
                UserBucket bucket = bucketNode.Value;
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    int rankInBucket = bucket.IndexOf(user);
                    Debug.Assert(rankInBucket >= 0);
                    rankCount += rankInBucket;
                    break;
                }

                rankCount += bucket.UserCount;
                bucketNode = bucketNode.Next;
            }

            return rankCount;
        }

        public User[] GetTopN(int topN)
        {
            topN = Math.Min(topN, _userCount);
            User[] result = new User[topN];
            int rankCount = 0;
            LinkedListNode<UserBucket>? bucketNode = _buckets.First;
            while (rankCount < topN)
            {
                UserBucket bucket = bucketNode!.Value;
                int n = Math.Min(bucket.UserCount, topN - rankCount);
                Array.Copy(bucket.Users, 0, result, rankCount, n);
                rankCount += n;
                bucketNode = bucketNode.Next;
            }

            return result;
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            int rankCount = 0;
            User user = _userDict[userId];

            LinkedListNode<UserBucket> bucketNode = _buckets.First!;
            UserBucket bucket;
            while (bucketNode != null)
            {
                bucket = bucketNode.Value;
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    break;
                }

                rankCount += bucket.UserCount;
                bucketNode = bucketNode.Next!;
            }

            Debug.Assert(bucketNode != null);
            bucket = bucketNode.Value;
            int inBucketIndex = bucket.IndexOf(user);
            Debug.Assert(inBucketIndex != -1);
            int resultRank = rankCount + inBucketIndex;
            int startRank = Math.Max(0, resultRank - aroundN);
            int endRank = Math.Min(resultRank + aroundN, _userCount - 1);
            int count = endRank - startRank + 1;

            while (rankCount > startRank)
            {
                bucketNode = bucketNode.Previous!;
                rankCount -= bucketNode.Value.UserCount;
            }

            inBucketIndex = startRank - rankCount;
            User[] result = new User[count];
            int resultIndex = 0;
            while (resultIndex < count)
            {
                bucket = bucketNode.Value;
                int n = Math.Min(bucket.UserCount - inBucketIndex, count - resultIndex);
                Array.Copy(bucket.Users, inBucketIndex, result, resultIndex, n);
                resultIndex += n;
                inBucketIndex = 0;
                bucketNode = bucketNode.Next!;
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
            for (LinkedListNode<UserBucket>? bucketNode = _buckets.First;
                 bucketNode != null;
                 bucketNode = bucketNode.Next)
            {
                Console.Write($"{bucketNode.Value.UserCount} ");
            }

            Console.WriteLine();
            Console.WriteLine("Each Bucket Score Range:");
            for (LinkedListNode<UserBucket>? bucketNode = _buckets.First;
                 bucketNode != null;
                 bucketNode = bucketNode.Next)
            {
                Console.WriteLine(
                    $"Bucket {(bucketNode.Value.MinUser).Score} - {(bucketNode.Value.MaxUser).Score}");
            }

            Console.WriteLine($"SplitCount: {_splitCount}");
            Console.WriteLine($"CombineCount: {_combineCount}");
        }
#endif
    }
}
/*
测试类: BucketLinkedListRankingList
基准测试类: BucketBRTreeRankingList
== Test stau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: AddUser
排行榜用户数: 200000
总耗时: 346 ms
平均耗时: 3.46 ms/1000操作
内存占用: 9.21 MB
内存峰值: 12.96 MB
测试日期: 2026/3/8 22:31:15
√ 所有操作结果验证通过！
总耗时: 346 ms vs 23 ms (+1404.35%)
平均耗时: 3.46 ms/1k操作 vs 0.23 ms/1k操作 (+1404.35%)
内存占用: 9.21 MB vs 9.30 MB (-0.96%)
内存峰值: 12.96 MB vs 13.05 MB (-0.66%)
== Test stau10w_10w End ===

== Test stgau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetAroundUser
排行榜用户数: 100000
总耗时: 358 ms
平均耗时: 3.58 ms/1000操作
内存占用: 36.66 MB
内存峰值: 36.68 MB
测试日期: 2026/3/8 22:31:16
√ 所有操作结果验证通过！
总耗时: 358 ms vs 52 ms (+588.46%)
平均耗时: 3.58 ms/1k操作 vs 0.52 ms/1k操作 (+588.46%)
内存占用: 36.66 MB vs 36.66 MB (0.00%)
内存峰值: 36.68 MB vs 36.68 MB (0.00%)
== Test stgau10w_10w End ===

== Test stgt10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetTopN
排行榜用户数: 100000
总耗时: 19 ms
平均耗时: 0.19 ms/1000操作
内存占用: 80.88 MB
内存峰值: 80.97 MB
测试日期: 2026/3/8 22:31:18
√ 所有操作结果验证通过！
总耗时: 19 ms vs 26 ms (-26.92%)
平均耗时: 0.19 ms/1k操作 vs 0.26 ms/1k操作 (-26.92%)
内存占用: 80.88 MB vs 80.88 MB (0.00%)
内存峰值: 80.97 MB vs 80.96 MB (+0.01%)
== Test stgt10w_10w End ===

== Test stgu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetUserRank
排行榜用户数: 100000
总耗时: 224 ms
平均耗时: 2.24 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:31:21
√ 所有操作结果验证通过！
总耗时: 224 ms vs 27 ms (+729.63%)
平均耗时: 2.24 ms/1k操作 vs 0.27 ms/1k操作 (+729.63%)
内存占用: 2.29 MB vs 2.29 MB (+0.02%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stgu10w_10w End ===

== Test stuu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: UpdateUser
排行榜用户数: 100000
总耗时: 290 ms
平均耗时: 2.90 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:31:21
√ 所有操作结果验证通过！
总耗时: 290 ms vs 59 ms (+391.53%)
平均耗时: 2.90 ms/1k操作 vs 0.59 ms/1k操作 (+391.53%)
内存占用: 2.29 MB vs 2.29 MB (0.00%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stuu10w_10w End ===

== Test t100w_100w ===
用户数: 1000000
操作数: 1000000
排行榜用户数: 1099921
总耗时: 14154 ms
平均耗时: 14.15 ms/1000操作
内存占用: 251.75 MB
内存峰值: 251.74 MB
测试日期: 2026/3/8 22:31:36
√ 所有操作结果验证通过！
总耗时: 14154 ms vs 544 ms (+2501.84%)
平均耗时: 14.15 ms/1k操作 vs 0.54 ms/1k操作 (+2501.84%)
内存占用: 251.75 MB vs 251.84 MB (-0.04%)
内存峰值: 251.74 MB vs 251.83 MB (-0.04%)
== Test t100w_100w End ===

== Test t10w_10w ===
用户数: 100000
操作数: 100000
排行榜用户数: 109905
总耗时: 106 ms
平均耗时: 1.06 ms/1000操作
内存占用: 29.07 MB
内存峰值: 32.82 MB
测试日期: 2026/3/8 22:31:44
√ 所有操作结果验证通过！
总耗时: 106 ms vs 23 ms (+360.87%)
平均耗时: 1.06 ms/1k操作 vs 0.23 ms/1k操作 (+360.87%)
内存占用: 29.07 MB vs 29.07 MB (-0.01%)
内存峰值: 32.82 MB vs 32.83 MB (-0.02%)
== Test t10w_10w End ===
*/
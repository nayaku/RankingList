using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RankingListNew
{
    public class BucketBiSkipListRankingList : IRankingList
    {
        private const int MaxLevel = 32; // 跳表的最大层数
        private const double P = 0.25; // 跳表的概率

        private BiSkipList _userList;
        private Dictionary<int, User> _userMap;

        public BucketBiSkipListRankingList(Span<User> users)
        {
            users.Sort();
            _userList = new BiSkipList(users);

            _userMap = new(users.Length);
            foreach (ref readonly User u in users)
            {
                _userMap[u.Id] = u;
            }
        }

        public BucketBiSkipListRankingList(List<User> users) :
            this(CollectionsMarshal.AsSpan(users))
        {
        }

        public int AddUser(User user)
        {
            Debug.Assert(!_userMap.ContainsKey(user.Id));
            _userMap.Add(user.Id, user);
            int rankCount = _userList.AddUser(user);

            return rankCount;
        }

        public int UpdateUser(User newUser)
        {
            User oldUser = _userMap[newUser.Id];
            _userList.RemoveUser(oldUser);
            int rankCount = _userList.AddUser(newUser);
            _userMap[newUser.Id] = newUser;
            return rankCount;
        }

        public int GetUserRank(int userId)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            User user = _userMap[userId];
            return _userList.GetUserRank(user);
        }

        public User[] GetTopN(int topN)
        {
            return _userList.GetTopN(topN);
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            User user = _userMap[userId];
            return _userList.GetAroundUser(user, aroundN);
        }

        public int GetRankingCount()
        {
            return _userList.Count;
        }

        public void DebugPrint()
        {
#if DEBUG
            _userList.DebugPrint();
#endif
        }

        // 参考：https://cloud.tencent.com/developer/article/2512982（不正确，level不对）
        // 参考：https://www.baeldung-cn.com/java-skiplist
        // 源码：https://github.com/tedcy/algorithm_test/blob/master/order_set/t_zset.h
        class BiSkipList
        {
            public BiSkipListNode Head;
            public int Count;
#if DEBUG
            private Random _random = new(2447);
            private int _addCount = 0;
            private int _addCompareCount = 0;
            private int _removeCount = 0;
            private int _removeCompareCount = 0;
            private int _getRankCount = 0;
            private int _getRankCompareCount = 0;
#else
            private Random _random = new();
#endif
            private int _level = 1;

            public BiSkipList(Span<User> initialUsers)
            {
                UserBucket[] buckets = BuildBucket(initialUsers);
                if (buckets.Length == 0)
                {
                    // 没有用户
                    UserBucket userBucket = new(new User[UserBucket.BucketSize], 0);
                    Head = new BiSkipListNode(userBucket, MaxLevel);
                    return;
                }
                else
                {
                    Head = new BiSkipListNode(buckets[0], MaxLevel);
                    BuildSkipList(buckets.AsSpan(1));
                }
                Count = initialUsers.Length;
            }

            private static UserBucket[] BuildBucket(Span<User> users)
            {
                // 初始化Bucket
                int bucketNum = (int)Math.Ceiling((double)users.Length / UserBucket.InitialBucketSize);
                UserBucket[] buckets = new UserBucket[bucketNum];
                for (int i = 0; i < bucketNum; i++)
                {
                    int l = i * UserBucket.InitialBucketSize;
                    int r = Math.Min((i + 1) * UserBucket.InitialBucketSize, users.Length);
                    int userCount = r - l;
                    User[] bucketUsers = new User[UserBucket.BucketSize];
                    users.Slice(l, userCount).CopyTo(bucketUsers);
                    buckets[i] = new UserBucket(bucketUsers, userCount);
                }

                return buckets;
            }

            private void BuildSkipList(Span<UserBucket> buckets)
            {
                // 构建跳表
                int[] userCount = new int[MaxLevel];
                BiSkipListNode[] currentLevelNodes = new BiSkipListNode[MaxLevel];
                for (int i = 0; i < MaxLevel; i++)
                {
                    userCount[i] = Head.UserBucket.UserCount;
                    currentLevelNodes[i] = Head;
                }
                foreach (var bucket in buckets)
                {
                    int randomLevel = RandomLevel();
                    BiSkipListNode newNode = new(bucket, randomLevel);
                    for (int i = 0; i < randomLevel; i++)
                    {
                        currentLevelNodes[i].Level[i].Next = newNode;
                        newNode.Level[i].Previous = currentLevelNodes[i];
                        newNode.Level[i].PreviousCount = userCount[i];
                        userCount[i] = 0;
                        currentLevelNodes[i] = newNode;
                    }
                    for (int i = 0; i < MaxLevel; i++)
                    {
                        userCount[i] += bucket.UserCount;
                    }
                }
                _level = MaxLevel;
                while (_level > 1 && Head.Level[_level - 1].Next == null)
                {
                    _level--;
                }
#if DEBUG
                Check();
#endif
            }

            private int RandomLevel()
            {
                int level = 1;
                while (_random.NextDouble() < P && level < MaxLevel)
                {
                    level++;
                }
                return level;
            }

            public int AddUser(User user)
            {
#if DEBUG
                _addCount++;
#endif
                int rankCount = 0;
                int[] userCount = new int[MaxLevel];
                BiSkipListNode[] update = new BiSkipListNode[MaxLevel];
                BiSkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null &&
                        current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next!;
                        userCount[i] += current.Level[i].PreviousCount;
#if DEBUG
                        _addCompareCount++;
#endif
                    }
                    rankCount += userCount[i];
                    // 增加区间用户数量
                    if (current.Level[i].Next != null)
                    {
                        current.Level[i].Next.Level[i].PreviousCount++;
                    }
                    update[i] = current;
                }

                int userIndexInBucket;
                UserBucket userBucket = current.UserBucket;
                if (!userBucket.Full)
                {
                    userIndexInBucket = userBucket.Insert(user);
                    if (userIndexInBucket == 0)
                    {
                        current.MinUser = user;
                    }
                }
                else
                {
                    UserBucket newBucket = userBucket.Split(user, out userIndexInBucket);
                    if (userIndexInBucket == 0)
                    {
                        current.MinUser = user;
                    }

                    int randomLevel = RandomLevel();
                    if (randomLevel > _level)
                    {
                        for (int i = _level; i < randomLevel; i++)
                        {
                            update[i] = Head;
                        }
                        _level = randomLevel;
                    }
                    BiSkipListNode newNode = new(newBucket, randomLevel);
                    int previousCount = userBucket.UserCount;
                    for (int i = 0; i < randomLevel; i++)
                    {
                        newNode.Level[i].Next = update[i].Level[i].Next;
                        update[i].Level[i].Next = newNode;
                        newNode.Level[i].Previous = update[i];
                        newNode.Level[i].PreviousCount = previousCount;
                        if (newNode.Level[i].Next != null)
                        {
                            newNode.Level[i].Next.Level[i].PreviousCount -= previousCount;
                            newNode.Level[i].Next.Level[i].Previous = newNode;
                        }
                        previousCount += userCount[i];
                    }
                }

                Count++;
#if DEBUG
                Check();
#endif

                return rankCount + userIndexInBucket;
            }

            public void RemoveUser(User user)
            {
#if DEBUG
                _removeCount++;
#endif
                int[] userCount = new int[_level];
                BiSkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null
                        && current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next!;
                        userCount[i] += current.Level[i].PreviousCount;
#if DEBUG                         
                        _removeCompareCount++;
#endif
                    }
                    // 减少区间用户数量
                    if (current.Level[i].Next != null)
                    {
                        current.Level[i].Next!.Level[i].PreviousCount--;
                    }
                }

                UserBucket userBucket = current.UserBucket;
                int userIndexInBucket = userBucket.Remove(user);
                bool needDelete = false;
                if (userBucket.Empty)
                {
                    needDelete = true;
                }
                else if (current.UserBucket.UserCount < UserBucket.CombineBucketSize
                         && current.Level[0].Previous?.UserBucket.UserCount < UserBucket.CombineBucketSize)
                {
                    current.Level[0].Previous!.UserBucket.Combine(current.UserBucket);
                    needDelete = true;
                }
                if (!needDelete)
                {
                    if (userIndexInBucket == 0)
                    {
                        current.MinUser = userBucket.MinUser;
                    }
                }
                else
                {
                    // Head节点不删除，保留一个空的桶
                    if (current != Head)
                    {
                        for (int i = 0; i < current.Level.Length; i++)
                        {
                            current.Level[i].Previous!.Level[i].Next = current.Level[i].Next;
                            if (current.Level[i].Next != null)
                            {
                                current.Level[i].Next!.Level[i].PreviousCount += current.Level[i].PreviousCount;
                                current.Level[i].Next!.Level[i].Previous = current.Level[i].Previous;
                            }
                        }
                        while (_level > 1 && Head.Level[_level - 1].Next == null)
                        {
                            _level--;
                        }
                    }
                }
                Count--;
#if DEBUG
                Check();
#endif
            }

            public int GetUserRank(User user)
            {
#if DEBUG
                _getRankCount++;
#endif
                int rankCount = 0;
                BiSkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null
                        && current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next!;
                        rankCount += current.Level[i].PreviousCount;
#if DEBUG
                        _getRankCompareCount++;
#endif
                    }
                }
                UserBucket userBucket = current.UserBucket;
                int userIndexInBucket = userBucket.IndexOf(user);
                Debug.Assert(userIndexInBucket >= 0, "用户不存在");
                return rankCount + userIndexInBucket;
            }

            public User[] GetTopN(int topN)
            {
                topN = Math.Min(topN, Count);
                User[] result = new User[topN];
                BiSkipListNode? current = Head;
                int rankCount = 0;
                while (rankCount < topN)
                {
                    Debug.Assert(current != null);
                    int n = Math.Min(current.UserBucket.UserCount, topN - rankCount);
                    Array.Copy(current.UserBucket.Users, 0, result, rankCount, n);
                    rankCount += n;
                    current = current.Level[0].Next;
                }
                return result;
            }

            public (User[], int) GetAroundUser(User user, int aroundN)
            {
                // 1. 找到对应的位置
                int rankCount = 0;
                BiSkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null
                        && current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next!;
                        rankCount += current.Level[i].PreviousCount;
                    }
                }
                UserBucket userBucket = current.UserBucket;
                int userIndexInBucket = userBucket.IndexOf(user);
                Debug.Assert(userIndexInBucket >= 0, "用户不存在");
                rankCount += userIndexInBucket;

                // 2. 准备结果
                int offset = 0; // 结果数组内的偏移，用于处理用户排名过靠前，存在数据空位的情况
                int leftNum = aroundN, rightNum = aroundN; // 需求数目
                if (rankCount < aroundN)
                {
                    // 用户排名过靠前，无法获取足够的左边用户
                    leftNum = rankCount;
                    offset = rankCount - aroundN;
                }
                if (rankCount + aroundN + 1 > Count)
                {
                    // 用户排名过靠后，无法获取足够的右边用户
                    rightNum = Count - rankCount - 1;
                }
                User[] result = new User[leftNum + rightNum + 1];

                // 3. 把桶内的用户填充到结果数组中
                // 左边计数
                int leftCount = Math.Min(userIndexInBucket, leftNum);
                // 右边计数
                int rightCount = Math.Min(userBucket.UserCount - userIndexInBucket - 1, rightNum);
                Array.Copy(userBucket.Users, userIndexInBucket - leftCount, result, aroundN - leftCount + offset,
                    leftCount + rightCount + 1);

                // 4. 获取缺少的用户
                BiSkipListNode tNode = current.Level[0].Previous!;
                while (leftCount < leftNum)
                {
                    userBucket = tNode.UserBucket!;
                    int n = Math.Min(userBucket.UserCount, leftNum - leftCount);
                    Array.Copy(userBucket.Users, userBucket.UserCount - n, result, aroundN - leftCount - n + offset, n);
                    leftCount += n;
                    tNode = tNode.Level[0].Previous;
                }
                tNode = current.Level[0].Next!;
                while (rightCount < rightNum)
                {
                    userBucket = tNode.UserBucket!;
                    int n = Math.Min(userBucket.UserCount, rightNum - rightCount);
                    Array.Copy(userBucket.Users, 0, result, aroundN + rightCount + 1 + offset, n);
                    rightCount += n;
                    tNode = tNode.Level[0].Next;
                }
                return (result, rankCount);
            }
#if DEBUG
            public void DebugPrint()
            {
                int[] levelCount = new int[MaxLevel];
                BiSkipListNode? current = Head;
                while (current != null)
                {
                    levelCount[current.Level.Length - 1]++;
                    current = current.Level[0].Next;
                }
                Console.WriteLine($"总用户数：{Count}");
                for (int i = 0; i < MaxLevel; i++)
                {
                    Console.Write($"L {i + 1}: {levelCount[i]}\t");
                }
                Console.WriteLine();
                Console.WriteLine($"总节点数：{levelCount.Sum()}");
                Console.WriteLine($"AddUser调用次数：{_addCount}，比较次数：{_addCompareCount}，平均比较次数：{(double)_addCompareCount / _addCount}");
                Console.WriteLine($"RemoveUser调用次数：{_removeCount}，比较次数：{_removeCompareCount}，平均比较次数：{(double)_removeCompareCount / _removeCount}");
                Console.WriteLine($"GetUserRank调用次数：{_getRankCount}，比较次数：{_getRankCompareCount}，平均比较次数：{(double)_getRankCompareCount / _getRankCount}");
            }

            private void Check()
            {
                BiSkipListNode[] update = new BiSkipListNode[MaxLevel];
                for (int i = 0; i < MaxLevel; i++)
                {
                    update[i] = Head;
                }
                int[] userCount = new int[MaxLevel];
                for (int i = 0; i < _level; i++)
                {
                    userCount[i] += Head.UserBucket.UserCount;
                }
                if (Count > 0)
                {
                    Debug.Assert(Head.MinUser.CompareTo(Head.UserBucket.MinUser) == 0, "头节点最小用户错误");
                }
                BiSkipListNode? current = Head.Level[0].Next;
                int nodeCount = 1;
                while (current != null)
                {
                    for (int i = 0; i < current.Level.Length; i++)
                    {
                        Debug.Assert(Head.MinUser.CompareTo(Head.UserBucket.MinUser) == 0, "节点最小用户错误");
                        Debug.Assert(current.Level[i].PreviousCount == userCount[i], "用户数量统计错误");
                        userCount[i] = 0;

                        Debug.Assert(update[i].Level[i].Next == current, "跳表连接错误");
                        Debug.Assert(current.Level[i].Previous == update[i], "跳表连接错误");
                        update[i] = current;
                    }

                    for (int i = 0; i < _level; i++)
                    {
                        userCount[i] += current.UserBucket.UserCount;
                    }

                    current = current.Level[0].Next;
                    nodeCount++;
                }
            }
#endif
        }

        class BiSkipListNode
        {
            public struct SkipListLevel
            {
                public BiSkipListNode? Next;
                public BiSkipListNode? Previous;
                public int PreviousCount; // 到前一个节点的用户数量（不包含本节点的用户数量）
            }
            public UserBucket UserBucket;
            public SkipListLevel[] Level;
            // 优化内存局部性，冗余存储每个节点的最小用户，避免访问UserBucket时的指针跳转
            public User MinUser;
#if DEBUG
            public static int TotalNodeCount = 1;
            public int Id;
#endif
            public BiSkipListNode(UserBucket bucket, int level)
            {
#if DEBUG
                Id = TotalNodeCount++;
#endif
                UserBucket = bucket;
                Level = new SkipListLevel[level];
                MinUser = bucket.MinUser;
            }
        }
    }
}
// 内存集中申请以后，性能有所提升
/*
测试类: BucketBiSkipListRankingList
基准测试类: BucketSkipListRankingList2
== Test t1w_100w ===
用户数: 10000
操作数: 1000000
排行榜用户数: 110059
总耗时: 614 ms
平均耗时: 0.61 ms/1000操作
内存占用: 258.66 MB
内存峰值: 258.98 MB
测试日期: 2026/3/13 17:51:40
√ 所有操作结果验证通过！
总耗时: 614 ms vs 664 ms (-7.53%)
平均耗时: 0.61 ms/1k操作 vs 0.66 ms/1k操作 (-7.53%)
内存占用: 258.66 MB vs 258.52 MB (+0.05%)
内存峰值: 258.98 MB vs 260.25 MB (-0.49%)
== Test t1w_100w End ===

== Test ta1w_50w ===
用户数: 10000
操作数: 500000
限制操作类型: AddUser
排行榜用户数: 510000
总耗时: 105 ms
平均耗时: 0.21 ms/1000操作
内存占用: 54.52 MB
内存峰值: 85.67 MB
测试日期: 2026/3/13 17:51:53
√ 所有操作结果验证通过！
总耗时: 105 ms vs 112 ms (-6.25%)
平均耗时: 0.21 ms/1k操作 vs 0.22 ms/1k操作 (-6.25%)
内存占用: 54.52 MB vs 54.46 MB (+0.11%)
内存峰值: 85.67 MB vs 85.59 MB (+0.10%)
== Test ta1w_50w End ===

== Test tga1w_50w ===
用户数: 10000
操作数: 500000
限制操作类型: GetAroundUser
排行榜用户数: 10000
总耗时: 128 ms
平均耗时: 0.26 ms/1000操作
内存占用: 183.15 MB
内存峰值: 183.17 MB
测试日期: 2026/3/13 17:51:54
√ 所有操作结果验证通过！
总耗时: 128 ms vs 180 ms (-28.89%)
平均耗时: 0.26 ms/1k操作 vs 0.36 ms/1k操作 (-28.89%)
内存占用: 183.15 MB vs 183.15 MB (0.00%)
内存峰值: 183.17 MB vs 183.17 MB (0.00%)
== Test tga1w_50w End ===

== Test tgt1w_50w ===
用户数: 10000
操作数: 500000
限制操作类型: GetTopN
排行榜用户数: 10000
总耗时: 188 ms
平均耗时: 0.38 ms/1000操作
内存占用: 404.31 MB
内存峰值: 404.32 MB
测试日期: 2026/3/13 17:52:16
√ 所有操作结果验证通过！
总耗时: 188 ms vs 309 ms (-39.16%)
平均耗时: 0.38 ms/1k操作 vs 0.62 ms/1k操作 (-39.16%)
内存占用: 404.31 MB vs 404.31 MB (0.00%)
内存峰值: 404.32 MB vs 411.34 MB (-1.71%)
== Test tgt1w_50w End ===

== Test tgu1w_50w ===
用户数: 10000
操作数: 500000
限制操作类型: GetUserRank
排行榜用户数: 10000
总耗时: 47 ms
平均耗时: 0.09 ms/1000操作
内存占用: 11.44 MB
内存峰值: 11.46 MB
测试日期: 2026/3/13 17:52:59
√ 所有操作结果验证通过！
总耗时: 47 ms vs 53 ms (-11.32%)
平均耗时: 0.09 ms/1k操作 vs 0.11 ms/1k操作 (-11.32%)
内存占用: 11.44 MB vs 11.44 MB (+0.01%)
内存峰值: 11.46 MB vs 11.46 MB (0.00%)
== Test tgu1w_50w End ===

== Test tu1w_50w ===
用户数: 10000
操作数: 500000
限制操作类型: UpdateUser
排行榜用户数: 10000
总耗时: 124 ms
平均耗时: 0.25 ms/1000操作
内存占用: 11.44 MB
内存峰值: 22.98 MB
测试日期: 2026/3/13 17:53:00
√ 所有操作结果验证通过！
总耗时: 124 ms vs 134 ms (-7.46%)
平均耗时: 0.25 ms/1k操作 vs 0.27 ms/1k操作 (-7.46%)
内存占用: 11.44 MB vs 11.44 MB (0.00%)
内存峰值: 22.98 MB vs 22.95 MB (+0.14%)
== Test tu1w_50w End ===

虽然比较次数较BucketBRTreeListRankingLis少，但是内存局部性较差，导致性能不如BucketBRTreeListRankingList。

AMDuProf测试显示：
test BucketBRTreeListRankingList -t 02-t100w_100 L1_DC_MISS_RATIO 0.009
test BucketBiSkipListRankingList -t 02-t100w_100 L1_DC_MISS_RATIO 0.017
L1数据缓冲占所有L1缓存访问的比例，数值越小表示内存局部性越好。
BucketSkipListRankingList3的L1数据缓冲命中率较低，可能是因为跳表节点的内存分布较为分散，导致CPU缓存效率较低。
相比之下，BucketBRTreeListRankingList的内存布局可能更有利于缓存，从而表现出更好的性能。

*/

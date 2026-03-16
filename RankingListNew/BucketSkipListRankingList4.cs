using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RankingListNew
{
    public class BucketSkipListRankingList4 : IRankingList
    {
        private static readonly int MaxLevel = 16; // 跳表的最大层数
        private static readonly double P = 0.5; // 跳表的概率
        private static readonly int BucketSize = 256; // 每个bucket的用户数量
        private static readonly int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量

        private SkipList _userList;
        private Dictionary<int, User> _userMap;

        public BucketSkipListRankingList4(Span<User> users)
        {
            users.Sort();
            _userList = new SkipList(users);

            _userMap = new(users.Length);
            foreach (ref readonly User u in users)
            {
                _userMap[u.Id] = u;
            }
        }

        public BucketSkipListRankingList4(List<User> users) :
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
        class SkipList
        {
            public SkipListNode Head;
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

            public SkipList(Span<User> initialUsers)
            {
                UserBucket[] buckets = BuildBucket(initialUsers);
                if (buckets.Length == 0)
                {
                    // 没有用户
                    UserBucket userBucket = new(new User[BucketSize], 0);
                    Head = new SkipListNode(userBucket, MaxLevel);
                    return;
                }
                else
                {
                    Head = new SkipListNode(buckets[0], MaxLevel);
                    BuildSkipList(buckets.AsSpan(1));
                }
                Count = initialUsers.Length;
            }

            private static UserBucket[] BuildBucket(Span<User> users)
            {
                // 初始化Bucket
                int bucketNum = (int)Math.Ceiling((double)users.Length / InitialBucketSize);
                UserBucket[] buckets = new UserBucket[bucketNum];
                for (int i = 0; i < bucketNum; i++)
                {
                    int l = i * InitialBucketSize;
                    int r = Math.Min((i + 1) * InitialBucketSize, users.Length);
                    int userCount = r - l;
                    User[] bucketUsers = new User[BucketSize];
                    users.Slice(l, userCount).CopyTo(bucketUsers);
                    buckets[i] = new UserBucket(bucketUsers, userCount);
                }

                return buckets;
            }

            private void BuildSkipList(Span<UserBucket> buckets)
            {
                // 构建跳表
                int[] userCount = new int[MaxLevel];
                SkipListNode[] currentLevelNodes = new SkipListNode[MaxLevel];
                for (int i = 0; i < MaxLevel; i++)
                {
                    userCount[i] = Head.UserBucket.UserCount;
                    currentLevelNodes[i] = Head;
                }
                foreach (var bucket in buckets)
                {
                    int randomLevel = RandomLevel();
                    SkipListNode newNode = new(bucket, randomLevel);
                    for (int i = 0; i < randomLevel; i++)
                    {
                        currentLevelNodes[i].Level[i].Next = newNode;
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
                int count = 0;
                int[] userCount = new int[MaxLevel];
                SkipListNode[] update = new SkipListNode[MaxLevel];
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null && current.Level[i].Next.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next;
                        userCount[i] += current.Level[i].PreviousCount;
#if DEBUG
                        _addCompareCount++;
#endif
                    }
                    count += userCount[i];
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
                    if (userIndexInBucket == userBucket.UserCount - 1)
                    {
                        current.MaxUser = user;
                    }
                }
                else
                {
                    UserBucket newBucket = userBucket.Split(user, out userIndexInBucket);
                    if (userIndexInBucket == 0)
                    {
                        current.MinUser = user;
                    }
                    current.MaxUser = userBucket.MaxUser;

                    int randomLevel = RandomLevel();
                    if (randomLevel > _level)
                    {
                        for (int i = _level; i < randomLevel; i++)
                        {
                            update[i] = Head;
                        }
                        _level = randomLevel;
                    }
                    SkipListNode newNode = new(newBucket, randomLevel);
                    int previousCount = userBucket.UserCount;
                    for (int i = 0; i < randomLevel; i++)
                    {
                        newNode.Level[i].Next = update[i].Level[i].Next;
                        update[i].Level[i].Next = newNode;
                        newNode.Level[i].PreviousCount = previousCount;
                        if (newNode.Level[i].Next != null)
                        {
                            newNode.Level[i].Next.Level[i].PreviousCount -= previousCount;
                        }
                        previousCount += userCount[i];
                    }
                }

                Count++;
#if DEBUG
                Check();
#endif

                return count + userIndexInBucket;
            }

            public void RemoveUser(User user)
            {
#if DEBUG
                _removeCount++;
#endif
                int i;
                int[] previousCount = new int[_level];
                SkipListNode[] update = new SkipListNode[_level];
                SkipListNode? previous = null;
                SkipListNode current = Head;
                if (user.CompareTo(Head.UserBucket.MaxUser) > 0) // 特判头节点
                {
                    for (i = _level - 1; i >= 0; i--)
                    {
                        while (current.Level[i].Next != null && current.Level[i].Next.MaxUser.CompareTo(user) < 0)
                        {
                            current = current.Level[i].Next;
                            previousCount[i] += current.Level[i].PreviousCount;
#if DEBUG
                            _removeCompareCount++;
#endif
                        }
                        update[i] = current;
                    }
                    previous = current;
                    current = current.Level[0].Next!;
                }

                UserBucket userBucket = current.UserBucket;
                int userIndexInBucket = userBucket.Remove(user);

                bool needDelete = false;
                if (Count > 1)
                {
                    if (!userBucket.Empty)
                    {
                        if (userIndexInBucket == 0)
                        {
                            current.MinUser = userBucket.MinUser;
                        }
                        if (userIndexInBucket == userBucket.UserCount)
                        {
                            current.MaxUser = userBucket.MaxUser;
                        }
                    }
                    if (userBucket.Empty)
                    {
                        needDelete = true;
                    }
                    else if (current.UserBucket.UserCount < BucketSize / 4 && previous != null
                        && previous.UserBucket.UserCount < BucketSize / 4)
                    {
                        previous.UserBucket.Combine(current.UserBucket);
                        previous.MaxUser = previous.UserBucket.MaxUser;
                        needDelete = true;
                    }
                    if (needDelete)
                    {
                        for (i = 0; i < current.Level.Length; i++)
                        {
                            update[i].Level[i].Next = current.Level[i].Next;
                            if (current.Level[i].Next != null)
                            {
                                current.Level[i].Next.Level[i].PreviousCount += current.Level[i].PreviousCount - 1;
                            }
                        }
                        while (_level > 1 && Head.Level[_level - 1].Next == null)
                        {
                            _level--;
                        }
                    }
                    else
                    {
                        for (i = 0; i < current.Level.Length; i++)
                        {
                            if (current.Level[i].Next != null)
                            {
                                current.Level[i].Next.Level[i].PreviousCount--;
                            }
                        }
                    }
                    // 更新区间用户数量
                    //for (; i < current.Level.Length; i++)
                    //{
                    //    if (current.Level[i].Next != null)
                    //    {
                    //        current.Level[i].Next.Level[i].PreviousCount--;
                    //    }
                    //}
                    for (; i < _level; i++)
                    {
                        if (update[i].Level[i].Next != null)
                        {
                            update[i].Level[i].Next.Level[i].PreviousCount--;
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
                int userCount = 0;
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null && current.Level[i].Next.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next;
                        userCount += current.Level[i].PreviousCount;
#if DEBUG
                        _getRankCompareCount++;
#endif
                    }
                }
                UserBucket userBucket = current.UserBucket;
                int userIndexInBucket = userBucket.IndexOf(user);
                Debug.Assert(userIndexInBucket >= 0, "用户不存在");
                return userCount + userIndexInBucket;
            }

            public User[] GetTopN(int topN)
            {
                topN = Math.Min(topN, Count);
                User[] result = new User[topN];
                SkipListNode current = Head;
                int userCount = 0;
                while (userCount < topN)
                {
                    Debug.Assert(current != null);
                    int n = Math.Min(current.UserBucket.UserCount, topN - userCount);
                    Array.Copy(current.UserBucket.Users, 0, result, userCount, n);
                    userCount += n;
                    current = current.Level[0].Next;
                }
                return result;
            }

            public (User[], int) GetAroundUser(User user, int aroundN)
            {
                // 1. 找到对应的位置
                int rankCount = 0;
                SkipListNode[] update = new SkipListNode[_level + 1]; // 特殊处理，为了找区间左端特判
                update[_level] = Head;
                int[] previousCount = new int[_level + 1];
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null && current.Level[i].Next.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next;
                        previousCount[i] += current.Level[i].PreviousCount;
                    }
                    update[i] = current;
                    rankCount += previousCount[i];
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
                SkipListNode tNode;
                // 左边缺少，左边的比较复杂
                if (leftCount < leftNum)
                {
                    int tLevel = 1;
                    int startRankCount = rankCount - leftNum;
                    int tRankCount = rankCount - userIndexInBucket - previousCount[0];
                    while (tRankCount > startRankCount)
                    {
                        tRankCount -= previousCount[tLevel];
                        tLevel++;
                    }
                    tNode = update[tLevel];
                    tLevel--;
                    for (; tLevel >= 0; tLevel--)
                    {
                        while (tRankCount + tNode.Level[tLevel].Next.Level[tLevel].PreviousCount <= startRankCount)
                        {
                            tNode = tNode.Level[tLevel].Next;
                            tRankCount += tNode.Level[tLevel].PreviousCount;
                        }
                    }
                    // 最左端的桶内，右侧部分
                    userBucket = tNode.UserBucket!;
                    int skipNum = startRankCount - tRankCount;
                    int n = userBucket.UserCount - skipNum;
                    Array.Copy(userBucket.Users, skipNum, result, 0, n);
                    tRankCount += userBucket.UserCount;
                    tNode = tNode.Level[0].Next!;
                    while (tRankCount + tNode.UserBucket.UserCount <= rankCount - userIndexInBucket)
                    {
                        userBucket = tNode.UserBucket!;
                        Array.Copy(userBucket.Users, 0, result, tRankCount - startRankCount, userBucket.UserCount);
                        tRankCount += userBucket.UserCount;
                        tNode = tNode.Level[0].Next!;
                    }
                }

                if (rightCount < rightNum)
                {
                    tNode = current.Level[0].Next!;
                    while (rightCount < rightNum)
                    {
                        userBucket = tNode.UserBucket!;
                        int n = Math.Min(userBucket.UserCount, rightNum - rightCount);
                        Array.Copy(userBucket.Users, 0, result, aroundN + rightCount + 1 + offset, n);
                        rightCount += n;
                        tNode = tNode.Level[0].Next!;
                    }
                }
                return (result, rankCount);
            }
#if DEBUG
            public void DebugPrint()
            {
                int[] levelCount = new int[MaxLevel];
                SkipListNode? current = Head;
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
                SkipListNode[] update = new SkipListNode[MaxLevel];
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
                    Debug.Assert(Head.MaxUser.CompareTo(Head.UserBucket.MaxUser) == 0, "头节点最大用户错误");
                }
                SkipListNode? current = Head.Level[0].Next;
                int nodeCount = 1;
                while (current != null)
                {
                    Debug.Assert(current.MinUser.CompareTo(current.UserBucket.MinUser) == 0, "节点最小用户错误");
                    Debug.Assert(current.MaxUser.CompareTo(current.UserBucket.MaxUser) == 0, "节点最大用户错误");
                    for (int i = 0; i < current.Level.Length; i++)
                    {
                        Debug.Assert(current.Level[i].PreviousCount == userCount[i], "用户数量统计错误");
                        userCount[i] = 0;

                        Debug.Assert(update[i].Level[i].Next == current, "跳表连接错误");
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

        class SkipListNode
        {
            public struct SkipListLevel
            {
                public SkipListNode? Next;
                public int PreviousCount; // 到前一个节点的用户数量（不包含本节点的用户数量）
            }
            public UserBucket UserBucket;
            public SkipListLevel[] Level;
            // 优化内存局部性，冗余存储每个节点的最小用户，避免访问UserBucket时的指针跳转
            public User MinUser;
            public User MaxUser;

#if DEBUG
            public static int TotalNodeCount = 1;
            public int Id;
#endif
            public SkipListNode(UserBucket bucket, int level)
            {
#if DEBUG
                Id = TotalNodeCount++;
#endif
                UserBucket = bucket;
                Level = new SkipListLevel[level];
                MinUser = bucket.MinUser;
                if (bucket.UserCount > 0)
                    MaxUser = bucket.MaxUser;
            }
        }

        class UserBucket
        {
            public User MinUser => Users[0];
            public User MaxUser => Users[UserCount - 1];
            public User[] Users;
            public int UserCount;
            public bool Full => UserCount >= Users.Length;
            public bool Empty => UserCount == 0;
            public int IndexOf(User user) => Array.BinarySearch(Users, 0, UserCount, user);

            public UserBucket(User[] users, int userCount)
            {
                Users = users;
                UserCount = userCount;
            }

            public int Insert(User user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                if (index < 0)
                {
                    index = ~index;
                }

                Array.Copy(Users, index, Users, index + 1, UserCount - index);
                Users[index] = user;
                UserCount++;
                return index;
            }

            public int Remove(User user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                Debug.Assert(index >= 0, "用户不存在");
                Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
                UserCount--;
                return index;
            }

            /// <summary>
            /// 分裂成两个桶
            /// </summary>
            /// <param name="user"></param>
            /// <param name="userIndex"></param>
            /// <returns>右边的新桶</returns>
            public UserBucket Split(User user, out int userIndex)
            {
                int mid = UserCount / 2;
                userIndex = Array.BinarySearch(Users, 0, UserCount, user);
                if (userIndex < 0)
                {
                    userIndex = ~userIndex;
                }

                User[] newUsers = new User[BucketSize];
                int newUserCount = UserCount - mid;
                if (userIndex >= mid)
                {
                    Array.Copy(Users, mid, newUsers, 0, userIndex - mid);
                    newUsers[userIndex - mid] = user;
                    Array.Copy(Users, userIndex, newUsers, userIndex - mid + 1, UserCount - userIndex);
                    newUserCount++;
                }
                else
                {
                    Array.Copy(Users, mid, newUsers, 0, UserCount - mid);
                }

                UserCount = mid;
                UserBucket newBucket = new(newUsers, newUserCount);
                if (userIndex < mid)
                    Insert(user);
                return newBucket;
            }

            public void Combine(UserBucket other)
            {
                Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
                UserCount += other.UserCount;
            }
        }
    }
}
// 做个单向跳表。
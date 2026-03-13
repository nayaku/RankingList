using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RankingListNew
{
    public class BlockSkipListRankingList2 : IRankingList
    {
        private static readonly int MaxLevel = 16; // 跳表的最大层数
        private static readonly double P = 0.5; // 跳表的概率
        private static readonly int BlockSize = 256; // 每个block的用户数量
        private static readonly int InitialBlockSize = BlockSize / 2; // 初始每个block的用户数量

        private SkipList _userList;
        private Dictionary<int, User> _userMap;

        public BlockSkipListRankingList2(Span<User> users)
        {
            users.Sort();
            _userList = new SkipList(users);

            _userMap = new(users.Length);
            foreach (ref readonly User u in users)
            {
                _userMap[u.Id] = u;
            }
        }

        public BlockSkipListRankingList2(List<User> users) :
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
                UserBlock[] blocks = BuildBlock(initialUsers);
                if (blocks.Length == 0)
                {
                    // 没有用户
                    UserBlock userBlock = new(new User[BlockSize], 0);
                    Head = new SkipListNode(userBlock, MaxLevel);
                    return;
                }
                else
                {
                    Head = new SkipListNode(blocks[0], MaxLevel);
                    BuildSkipList(blocks.AsSpan(1));
                }
                Count = initialUsers.Length;
            }

            private static UserBlock[] BuildBlock(Span<User> users)
            {
                // 初始化Block
                int blockNum = (int)Math.Ceiling((double)users.Length / InitialBlockSize);
                UserBlock[] blocks = new UserBlock[blockNum];
                for (int i = 0; i < blockNum; i++)
                {
                    int l = i * InitialBlockSize;
                    int r = Math.Min((i + 1) * InitialBlockSize, users.Length);
                    int userCount = r - l;
                    User[] blockUsers = new User[BlockSize];
                    users.Slice(l, userCount).CopyTo(blockUsers);
                    blocks[i] = new UserBlock(blockUsers, userCount);
                }

                return blocks;
            }

            private void BuildSkipList(Span<UserBlock> blocks)
            {
                // 构建跳表
                int[] userCount = new int[MaxLevel];
                SkipListNode[] currentLevelNodes = new SkipListNode[MaxLevel];
                for (int i = 0; i < MaxLevel; i++)
                {
                    userCount[i] = Head.UserBlock.UserCount;
                    currentLevelNodes[i] = Head;
                }
                foreach (var block in blocks)
                {
                    int randomLevel = RandomLevel();
                    SkipListNode newNode = new(block, randomLevel);
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
                        userCount[i] += block.UserCount;
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
                    update[i] = current;
                    // 增加区间用户数量
                    if (current.Level[i].Next != null)
                    {
                        current.Level[i].Next.Level[i].PreviousCount++;
                    }
                }

                int count = userCount.Sum(), userIndexInBlock;
                UserBlock userBlock = current.UserBlock;
                if (!userBlock.Full)
                {
                    userIndexInBlock = userBlock.Insert(user);
                }
                else
                {
                    UserBlock newBlock = userBlock.Split(user, out userIndexInBlock);

                    int randomLevel = RandomLevel();
                    if (randomLevel > _level)
                    {
                        for (int i = _level; i < randomLevel; i++)
                        {
                            update[i] = Head;
                        }
                        _level = randomLevel;
                    }
                    SkipListNode newNode = new(newBlock, randomLevel);
                    int previousCount = userBlock.UserCount;
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

                return count + userIndexInBlock;
            }

            public void RemoveUser(User user)
            {
#if DEBUG
                _removeCount++;
#endif
                int[] userCount = new int[MaxLevel];
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null && current.Level[i].Next.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next;
                        userCount[i] += current.Level[i].PreviousCount;
#if DEBUG                         
                        _removeCompareCount++;
#endif
                    }
                    // 减少区间用户数量
                    if (current.Level[i].Next != null)
                    {
                        current.Level[i].Next.Level[i].PreviousCount--;
                    }
                }

                UserBlock userBlock = current.UserBlock;
                userBlock.Remove(user);
                bool needDelete = false;
                if (Count > 1)
                {
                    if (userBlock.Empty)
                    {
                        needDelete = true;
                    }
                    else if (current.UserBlock.UserCount < BlockSize / 4
                        && current.Level[0].Previous?.UserBlock.UserCount < BlockSize / 4)
                    {
                        current.Level[0].Previous.UserBlock.Combine(current.UserBlock);
                        needDelete = true;
                    }
                    if (needDelete)
                    {
                        for (int i = 0; i < current.Level.Length; i++)
                        {
                            current.Level[i].Previous.Level[i].Next = current.Level[i].Next;
                            if (current.Level[i].Next != null)
                            {
                                current.Level[i].Next.Level[i].PreviousCount += current.Level[i].PreviousCount;
                                current.Level[i].Next.Level[i].Previous = current.Level[i].Previous;
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
                UserBlock userBlock = current.UserBlock;
                int userIndexInBlock = userBlock.IndexOf(user);
                Debug.Assert(userIndexInBlock >= 0, "用户不存在");
                return userCount + userIndexInBlock;
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
                    int n = Math.Min(current.UserBlock.UserCount, topN - userCount);
                    Array.Copy(current.UserBlock.Users, 0, result, userCount, n);
                    userCount += n;
                    current = current.Level[0].Next;
                }
                return result;
            }

            public (User[], int) GetAroundUser(User user, int aroundN)
            {
                // 1. 找到对应的位置
                int rankCount = 0;
                SkipListNode[] update = new SkipListNode[MaxLevel];
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Level[i].Next != null && current.Level[i].Next.MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Level[i].Next;
                        rankCount += current.Level[i].PreviousCount;
                    }
                }
                UserBlock userBlock = current.UserBlock;
                int userIndexInBlock = userBlock.IndexOf(user);
                Debug.Assert(userIndexInBlock >= 0, "用户不存在");
                rankCount += userIndexInBlock;

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

                // 3. 把块内的用户填充到结果数组中
                // 左边计数
                int leftCount = Math.Min(userIndexInBlock, leftNum);
                // 右边计数
                int rightCount = Math.Min(userBlock.UserCount - userIndexInBlock - 1, rightNum);
                Array.Copy(userBlock.Users, userIndexInBlock - leftCount, result, aroundN - leftCount + offset,
                    leftCount + rightCount + 1);

                // 4. 获取缺少的用户
                SkipListNode tNode = current.Level[0].Previous!;
                while (leftCount < leftNum)
                {
                    userBlock = tNode.UserBlock!;
                    int n = Math.Min(userBlock.UserCount, leftNum - leftCount);
                    Array.Copy(userBlock.Users, userBlock.UserCount - n, result, aroundN - leftCount - n + offset, n);
                    leftCount += n;
                    tNode = tNode.Level[0].Previous!;
                }
                tNode = current.Level[0].Next!;
                while (rightCount < rightNum)
                {
                    userBlock = tNode.UserBlock!;
                    int n = Math.Min(userBlock.UserCount, rightNum - rightCount);
                    Array.Copy(userBlock.Users, 0, result, aroundN + rightCount + 1 + offset, n);
                    rightCount += n;
                    tNode = tNode.Level[0].Next!;
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
                    userCount[i] += Head.UserBlock.UserCount;
                }
                SkipListNode? current = Head.Level[0].Next;
                int nodeCount = 1;
                while (current != null)
                {
                    for (int i = 0; i < current.Level.Length; i++)
                    {
                        Debug.Assert(current.Level[i].PreviousCount == userCount[i], "用户数量统计错误");
                        userCount[i] = 0;

                        Debug.Assert(update[i].Level[i].Next == current, "跳表连接错误");
                        Debug.Assert(current.Level[i].Previous == update[i], "跳表连接错误");
                        update[i] = current;
                    }

                    for (int i = 0; i < _level; i++)
                    {
                        userCount[i] += current.UserBlock.UserCount;
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
                public SkipListNode? Previous;
                public int PreviousCount; // 到前一个节点的用户数量（不包含本节点的用户数量）
            }
            public UserBlock UserBlock;
            public SkipListLevel[] Level;
            public User MinUser => UserBlock.MinUser;

#if DEBUG
            public static int TotalNodeCount = 1;
            public int Id;
#endif
            public SkipListNode(UserBlock block, int level)
            {
#if DEBUG
                Id = TotalNodeCount++;
#endif
                UserBlock = block;
                Level = new SkipListLevel[level];
            }
        }

        class UserBlock
        {
            public User MinUser => Users[0];
            public User MaxUser => Users[UserCount - 1];
            public User[] Users;
            public int UserCount;
            public bool Full => UserCount >= Users.Length;
            public bool Empty => UserCount == 0;
            public int IndexOf(User user) => Array.BinarySearch(Users, 0, UserCount, user);

            public UserBlock(User[] users, int userCount)
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
            /// 分裂成两个块
            /// </summary>
            /// <param name="user"></param>
            /// <param name="userIndex"></param>
            /// <returns>右边的新块</returns>
            public UserBlock Split(User user, out int userIndex)
            {
                int mid = UserCount / 2;
                userIndex = Array.BinarySearch(Users, 0, UserCount, user);
                if (userIndex < 0)
                {
                    userIndex = ~userIndex;
                }

                User[] newUsers = new User[BlockSize];
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
                UserBlock newBlock = new(newUsers, newUserCount);
                if (userIndex < mid)
                    Insert(user);
                return newBlock;
            }

            public void Combine(UserBlock other)
            {
                Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
                UserCount += other.UserCount;
            }
        }
    }
}
// 内存集中申请以后，性能有所提升
/*
测试类: BlockBRTreeRankingList
== Test t1w_100w ===
用户数: 10000
操作数: 1000000
排行榜用户数: 110059
总耗时: 695 ms
平均耗时: 0.70 ms/1000操作
内存占用: 259.37 MB
内存峰值: 262.94 MB
测试日期: 2026/3/13 13:35:42
9-134  9-126  9-128  9-128  9-129  9-127  9-128  10-131  10-127  9-127  9-127  9-129  10-129  10-131  9-122  9-131  9-124  10-131  10-129  9-130  9-123  9-131  10-130  10-126  9-132  9-121  9-128  10-132  10-129  9-125  9-134  9-123  10-124  10-132  9-130  9-127  9-125  10-129  10-128  9-127  9-127  9-132  10-127  10-127  9-126  9-129  9-132  10-123  10-130  9-128  9-132  9-122  10-129  10-128  9-127  9-130  9-131  10-125  10-127  8-129  8-129  8-127  9-128  9-127  8-127  8-132  8-123  9-136  9-126  10-123  10-132  10-129  11-129  11-127  9-128  9-133  10-128  11-168  11-247  10-243  11-130  11-149  11-121  11-160  11-162  11-155  11-130  11-134  11-136  11-156  10-192  11-134  11-169  10-231  11-146  11-226  11-139  11-135  11-161  12-133  12-125  11-157  11-142  11-164  12-179  12-135  11-192  11-181  11-191  12-156  12-142  11-199  12-183  12-183  11-232  12-207  12-209  11-233  11-237  12-127  12-133  12-231  12-249  12-228  13-125  13-136  12-125  12-137  12-101  12-190  12-104  12-194  12-114  12-192  12-108  12-175  11-75  12-128  12-133  12-97  12-204  12-105  12-194  12-72  13-113  13-174  12-64  13-116  13-178  12-68  13-120  13-167  12-78  13-128  13-130  11-65  12-112  12-201  11-53  12-91  13-126  13-157  11-51  12-78  13-107  13-214  10-33  10-35  11-48  11-69  11-88  12-109  13-127  13-135  10-60  11-55  11-78  10-79  11-106  12-126  12-180  9-58  10-44  10-60  9-82  10-96  11-119  11-217  10-32  10-40  11-64  11-79  11-85  12-114  13-128  13-129  10-63  11-47  11-70  10-86  11-96  12-122  12-198  9-59  10-52  10-67  10-79  10-92  10-110  11-125  11-152  9-55  10-59  10-75  9-86  10-103  11-110  11-224  10-24  10-45  11-45  11-69  11-79  12-97  13-118  13-229  10-28  10-36  11-61  11-73  11-94  12-103  13-114  13-216  10-37  10-32  11-51  11-66  11-78  12-97  13-115  13-250  10-29  10-36  11-61  11-67  11-92  12-112  12-223  10-56  11-53  11-74  10-73  11-92  12-112  12-239  10-55  11-46  11-72  10-84  11-93  12-114  12-231  9-61  10-41  10-64  10-78  10-90  10-104  11-125  11-138  10-55  11-59  11-58  11-72  11-100  11-113  12-123  12-147  10-50  11-49  11-62  10-79  11-93  12-110  12-254  10-55  11-44  11-68  10-85  11-99  12-121  12-189  10-60  11-48  11-64  10-87  11-105  12-127  12-131  9-43  10-37  10-61  10-72  10-91  10-106  11-124  11-152  9-59  10-57  10-74  9-88  10-108  11-123  11-218  9-20  9-44  10-63  10-64  10-76  11-92  12-112  12-211  9-34  9-37  10-58  10-66  11-76  11-99  11-109  12-128  12-139  10-35  10-36  11-55  11-70  11-86  12-109  13-124  13-161  10-21  10-48  11-50  11-67  11-81  12-97  13-113  13-231  9-51  10-49  10-54  10-77  10-93  10-112  11-127  11-139  9-62  10-58  10-60  9-81  10-102  11-113  11-236  9-31  9-37  10-50  10-70  11-81  11-84  11-108  12-125  12-155  10-53  11-49  11-61  11-70  11-92  11-108  12-128  12-146  10-54  11-51  11-69  10-81  11-98  12-105  12-228  10-22  10-43  11-42  11-54  12-76  12-84  12-98  13-121  13-203  10-38  11-54  11-56  11-70  11-88  11-103  12-117  12-188  10-56  11-52  11-62  10-71  11-98  12-112  12-247  10-61  11-51  11-65  11-85  11-91  11-107  12-126  12-142  9-44  10-32  10-58  10-64  10-81  10-102  11-109  11-243  8-52  9-41  9-54  9-71  9-90  9-93  10-109  10-203  9-29  9-38  10-58  10-63  10-86  11-100  12-117  12-225  9-50  10-38  10-68  10-66  10-79  10-105  11-118  11-219  9-59  10-48  10-73  10-71  10-85  10-101  11-114  11-246  9-41  9-44  10-52  10-72  10-80  11-101  12-117  12-205  10-27  10-44  11-45  11-72  11-75  12-88  13-112  13-242  10-37  10-37  11-53  11-73  11-86  12-106  13-122  13-198  10-36  10-47  11-44  11-53  12-74  12-90  12-107  13-120  13-198  10-57  11-54  11-55  11-76  11-84  11-104  12-121  12-200  8-49  9-39  9-60  9-64  9-84  10-89  11-105  11-242  9-23  9-32  9-40  10-51  10-57  11-85  11-95  11-107  12-123  12-154  9-48  10-51  10-58  10-70  10-97  10-100  11-121  11-246  9-52  10-41  10-62  10-72  10-87  10-96  11-116  11-219  9-26  9-42  10-38  10-59  11-68  11-94  11-105  12-124  12-199  9-58  10-39  10-48  10-68  10-76  10-94  11-104  12-127  12-140  9-48  10-44  10-62  10-74  10-85  10-103  11-113  11-228  10-39  10-32  11-54  11-66  12-74  12-96  12-103  13-122  13-161  10-41  11-45  11-45  11-65  11-77  11-96  12-116  13-124  13-148  9-43  10-37  10-45  10-65  10-93  11-87  11-100  12-123  12-230  11-16  9-27  9-44  10-51  10-73  11-79  11-98  11-110  12-126  12-144  9-47  10-45  10-56  10-77  10-80  10-102  11-117  12-126  12-148  10-41  11-44  11-57  11-67  11-90  11-96  12-118  12-235  10-40  11-40  11-54  11-55  11-76  11-105  12-109  13-124  13-183  10-51  11-38  11-67  11-66  11-79  11-99  12-106  12-248  10-49  11-53  11-52  11-72  11-77  11-89  12-111  13-124  13-179  10-52  11-46  11-52  11-70  11-87  11-98  12-110  13-127  13-132  10-36  11-37  11-57  11-59  11-70  12-87  12-101  12-109  13-121  13-151  10-46  11-42  11-55  11-71  11-85  11-92  12-109  12-248  10-48  11-42  11-51  11-69  11-73  11-87  12-101  13-121  13-221  10-51  11-32  11-55  11-63  11-76  11-95  12-97  13-118  13-196  10-38  11-35  11-42  11-51  11-66  12-77  12-94  12-108  13-117  13-204  10-50  11-37  11-43  11-64  11-69  11-76  12-101  13-113  13-229  10-48  11-45  11-53  11-67  11-76  11-84  12-108  13-117  13-232  10-51  11-27  11-49  11-61  11-75  11-94  12-102  13-120  13-211  10-43  11-46  11-56  11-57  11-73  12-83  12-102  12-116  13-119  13-191  10-39  11-36  11-48  11-51  11-73  12-86  12-94  12-111  13-125  13-165  10-38  11-37  11-52  11-60  11-70  12-73  12-102  12-113  13-126  13-154  10-49  11-35  11-32  10-47  10-65  10-71  10-85  11-101  12-119  12-228  10-40  10-29  10-39  11-50  11-64  12-71  12-86  12-103  13-115  13-246  10-42  11-31  11-47  11-65  11-60  12-82  12-89  12-99  13-118  13-217  11-38  11-62  11-44  11-60  11-69  11-89  11-98  12-105  13-122  13-153  11-37  11-63  11-45  11-56  11-74  11-85  11-107  12-112  13-124  13-175  10-49  11-32  11-48  11-57  11-58  12-82  12-87  12-110  13-125  13-216  11-31  11-59  11-50  11-61  11-78  11-87  11-96  12-99  13-122  13-181  10-41  11-29  11-37  10-58  10-54  10-67  10-90  10-89  11-111  12-121  12-188  11-36  12-35  12-36  11-57  11-63  11-71  11-86  11-93  12-107  13-123  13-210  11-45  12-29  12-44  11-45  11-60  11-77  11-76  11-90  12-109  13-113  13-228  11-46  12-41  12-38  11-45  11-72  11-76  11-93  12-100  12-107  13-121  13-188  12-21  11-60  12-25  12-53  11-51  11-69  11-81  11-97  12-109  13-124  13-199  11-5  11-36  12-34  12-42  11-58  11-66  11-67  11-87  11-107  12-113  13-117  13-223  11-28  12-35  12-46  11-45  11-57  11-63  11-68  12-101  12-96  12-114  13-127  13-160  10-49  11-32  11-32  10-49  10-48  10-61  10-76  11-87  11-93  11-99  12-113  12-246  10-33  11-33  11-42  10-43  10-51  10-63  10-82  11-72  11-86  11-93  12-110  13-115  13-199  10-26  10-57  10-37  10-43  10-55  10-66  11-71  11-82  11-108  12-108  13-121  13-229  10-33  11-31  11-43  10-45  10-57  10-56  10-70  11-84  11-89  11-88  12-114  13-119  13-227  9-39  9-48  9-34  9-48  9-49  9-58  10-71  10-85  11-79  11-100  11-113  12-121  12-199  9-28  9-56  9-39  9-51  9-59  9-64  10-70  10-81  11-89  11-102  11-113  12-120  12-248  10-18  10-50  10-24  10-48  11-53  11-47  11-63  11-72  11-71  11-89  12-102  12-102  12-114  13-126  13-172  11-29  11-43  10-63  11-41  11-57  11-56  11-66  10-69  10-85  11-76  11-97  11-104  12-114  13-123  13-205  9-53  9-56  10-36  10-49  10-63  10-65  9-60  9-75  10-72  10-92  11-92  11-112  11-116  12-126  12-178  9-23  9-43  9-22  9-44  10-38  10-33  10-51  10-53  10-73  10-83  11-81  11-84  12-100  12-107  12-114  13-127  13-191  9-48  9-50  10-34  10-36  10-44  10-49  10-56  10-70  10-62  10-66  10-75  10-83  11-103  11-99  11-110  12-121  12-223  8-56  8-45  9-38  9-39  9-39  9-48  9-65  9-56  9-52  9-68  9-82  9-83  10-80  10-99  11-106  11-105  11-118  12-126  12-174  9-34  9-40  9-49  10-38  10-35  9-46  9-50  9-49  9-52  9-56  9-51  9-58  9-69  9-76  9-78  10-81  10-84  11-100  11-110  11-118  12-119  12-240  8-55  9-41  9-47  8-55  9-34  9-35  9-32  9-38  9-46  9-52  9-54  9-57  9-60  9-65  8-65  8-82  8-81  8-79  9-96  9-86  9-100  9-90  10-112  10-117  11-114  11-124  12-127  12-148  11-2  10-42  10-30  9-43  8-30  8-39  8-35  8-38  8-44  8-36  8-60  9-27  9-37  8-36  8-36  8-28  8-39  8-29  8-52  8-33  8-41  9-52  9-47  9-61  9-49  9-45  9-45  9-56  9-47  9-63  9-63  9-63  9-74  9-63  9-64  9-63  9-83  9-65  9-83  9-88  9-85  9-95  9-88  9-89  9-89  9-92  9-92  9-90  9-95  10-99  10-96  10-112  10-105  10-106  10-108  11-117  11-117  12-118  12-123  12-121  13-128  13-170  
AddUser调用次数：300481，比较次数：2972286，平均比较次数：9.891760211128158
RemoveUser调用次数：200422，比较次数：1879059，平均比较次数：9.37551266826995
GetUserRank调用次数：299509，比较次数：2806926，平均比较次数：9.371758444654418
== Test t1w_100w End ===

测试类: BlockSkipListRankingList2
== Test t1w_100w ===
用户数: 10000
操作数: 1000000
排行榜用户数: 110059
总耗时: 10489 ms
平均耗时: 10.49 ms/1000操作
内存占用: 258.58 MB
内存峰值: 268.54 MB
测试日期: 2026/3/13 13:37:10
总用户数：110059
L 1: 495        L 2: 250        L 3: 145        L 4: 84 L 5: 23 L 6: 9  L 7: 6  L 8: 3  L 9: 5  L 10: 1 L 11: 1 L 12: 0
L 13: 0 L 14: 0 L 15: 0 L 16: 1
总节点数：1023
AddUser调用次数：300481，比较次数：2415310，平均比较次数：8.03814550670425
RemoveUser调用次数：200422，比较次数：1544652，平均比较次数：7.706998233726837
GetUserRank调用次数：299509，比较次数：2306544，平均比较次数：7.7010841076561976
== Test t1w_100w End ===

比较次数虽然减少，但是性能却大幅下降，可能是因为跳表节点的内存分配和访问效率较低，导致整体性能不如之前的实现。
*/
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RankingListNew
{
    public class BlockSkipListRankingList : IRankingList
    {
        private static readonly int MaxLevel = 32; // 跳表的最大层数
        private static readonly double P = 0.5; // 跳表的概率
        private static readonly int BlockSize = 256; // 每个block的用户数量
        private static readonly int InitialBlockSize = BlockSize / 2; // 初始每个block的用户数量

        private SkipList _userList;
        private Dictionary<int, User> _userMap;

        public BlockSkipListRankingList(Span<User> users)
        {
            users.Sort();
            _userList = new SkipList(users);

            _userMap = new(users.Length);
            foreach (ref readonly User u in users)
            {
                _userMap[u.Id] = u;
            }
        }

        public BlockSkipListRankingList(List<User> users) :
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
        class SkipList
        {
            public SkipListNode Head;
            public int Count;
#if DEBUG
            private Random _random = new(2447);
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
                        currentLevelNodes[i].Next[i] = newNode;
                        newNode.Previous[i] = currentLevelNodes[i];
                        newNode.PreviousCount[i] = userCount[i];
                        userCount[i] = 0;
                        currentLevelNodes[i] = newNode;
                    }
                    for (int i = 0; i < MaxLevel; i++)
                    {
                        userCount[i] += block.UserCount;
                    }
                }
                _level = MaxLevel;
                while (_level > 1 && Head.Next[_level - 1] == null)
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
                int[] userCount = new int[MaxLevel];
                SkipListNode[] update = new SkipListNode[MaxLevel];
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Next[i];
                        userCount[i] += current.PreviousCount[i];
                    }
                    update[i] = current;
                    // 增加区间用户数量
                    if (current.Next[i] != null)
                    {
                        current.Next[i].PreviousCount[i]++;
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
                        newNode.Next[i] = update[i].Next[i];
                        update[i].Next[i] = newNode;
                        newNode.Previous[i] = update[i];
                        newNode.PreviousCount[i] = previousCount;
                        if (newNode.Next[i] != null)
                        {
                            newNode.Next[i].PreviousCount[i] -= previousCount;
                            newNode.Next[i].Previous[i] = newNode;
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
                int[] userCount = new int[MaxLevel];
                //SkipListNode[] update = new SkipListNode[MaxLevel];
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Next[i];
                        userCount[i] += current.PreviousCount[i];
                    }
                    //update[i] = current;
                    // 减少区间用户数量
                    if (current.Next[i] != null)
                    {
                        current.Next[i].PreviousCount[i]--;
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
                        && current.Previous[0]?.UserBlock.UserCount < BlockSize / 4)
                    {
                        current.Previous[0].UserBlock.Combine(current.UserBlock);
                        needDelete = true;
                    }
                    if (needDelete)
                    {
                        for (int i = 0; i < current.Previous.Length; i++)
                        {
                            current.Previous[i]!.Next[i] = current.Next[i];
                            if (current.Next[i] != null)
                            {
                                current.Next[i].PreviousCount[i] += current.PreviousCount[i];
                                current.Next[i].Previous[i] = current.Previous[i];
                            }
                        }
                        while (_level > 1 && Head.Next[_level - 1] == null)
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
                int userCount = 0;
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Next[i];
                        userCount += current.PreviousCount[i];
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
                    current = current.Next[0];
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
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) <= 0)
                    {
                        current = current.Next[i];
                        rankCount += current.PreviousCount[i];
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
                SkipListNode tNode = current.Previous[0]!;
                while (leftCount < leftNum)
                {
                    userBlock = tNode.UserBlock!;
                    int n = Math.Min(userBlock.UserCount, leftNum - leftCount);
                    Array.Copy(userBlock.Users, userBlock.UserCount - n, result, aroundN - leftCount - n + offset, n);
                    leftCount += n;
                    tNode = tNode.Previous[0];
                }
                tNode = current.Next[0]!;
                while (rightCount < rightNum)
                {
                    userBlock = tNode.UserBlock!;
                    int n = Math.Min(userBlock.UserCount, rightNum - rightCount);
                    Array.Copy(userBlock.Users, 0, result, aroundN + rightCount + 1 + offset, n);
                    rightCount += n;
                    tNode = tNode.Next[0];
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
                    levelCount[current.Next.Length - 1]++;
                    current = current.Next[0];
                }
                Console.WriteLine($"总用户数：{Count}");
                for (int i = 0; i < MaxLevel; i++)
                {
                    Console.WriteLine($"Level {i + 1}: {levelCount[i]}");
                }
                Console.WriteLine($"总节点数：{levelCount.Sum()}");
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
                SkipListNode? current = Head.Next[0];
                int nodeCount = 1;
                while (current != null)
                {
                    for (int i = 0; i < current.PreviousCount.Length; i++)
                    {
                        Debug.Assert(current.PreviousCount[i] == userCount[i], "用户数量统计错误");
                        userCount[i] = 0;

                        Debug.Assert(update[i].Next[i] == current, "跳表连接错误");
                        Debug.Assert(current.Previous[i] == update[i], "跳表连接错误");
                        update[i] = current;
                    }

                    for (int i = 0; i < _level; i++)
                    {
                        userCount[i] += current.UserBlock.UserCount;
                    }

                    current = current.Next[0];
                    nodeCount++;
                }
            }
#endif
        }

        class SkipListNode
        {
            public UserBlock UserBlock;
            public SkipListNode?[] Next;
            public SkipListNode?[] Previous;
            // 每一层到前一个节点的用户数量（不包含本节点的用户数量）
            public int[] PreviousCount;
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
                Next = new SkipListNode[level];
                PreviousCount = new int[level];
                Previous = new SkipListNode[level];
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
// 跳表单线程下也不是最优解：https://weakyon.com/2022/10/09/performance-of-skip-list.html
/*
测试类: BlockSkipListRankingList
基准测试类: BucketBRTreeRankingList
== Test stau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: AddUser
排行榜用户数: 200000
总耗时: 61 ms
平均耗时: 0.61 ms/1000操作
内存占用: 9.29 MB
内存峰值: 24.47 MB
测试日期: 2026/3/12 20:27:08
√ 所有操作结果验证通过！
总耗时: 61 ms vs 23 ms (+165.22%)
平均耗时: 0.61 ms/1k操作 vs 0.23 ms/1k操作 (+165.22%)
内存占用: 9.29 MB vs 9.30 MB (-0.11%)
内存峰值: 24.47 MB vs 13.05 MB (+87.52%)
== Test stau10w_10w End ===

== Test stgau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetAroundUser
排行榜用户数: 100000
总耗时: 77 ms
平均耗时: 0.77 ms/1000操作
内存占用: 36.67 MB
内存峰值: 42.51 MB
测试日期: 2026/3/12 20:27:08
√ 所有操作结果验证通过！
总耗时: 77 ms vs 52 ms (+48.08%)
平均耗时: 0.77 ms/1k操作 vs 0.52 ms/1k操作 (+48.08%)
内存占用: 36.67 MB vs 36.66 MB (+0.02%)
内存峰值: 42.51 MB vs 36.68 MB (+15.90%)
== Test stgau10w_10w End ===

== Test stgt10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetTopN
排行榜用户数: 100000
总耗时: 18 ms
平均耗时: 0.18 ms/1000操作
内存占用: 80.88 MB
内存峰值: 80.92 MB
测试日期: 2026/3/12 20:27:10
√ 所有操作结果验证通过！
总耗时: 18 ms vs 26 ms (-30.77%)
平均耗时: 0.18 ms/1k操作 vs 0.26 ms/1k操作 (-30.77%)
内存占用: 80.88 MB vs 80.88 MB (0.00%)
内存峰值: 80.92 MB vs 80.96 MB (-0.05%)
== Test stgt10w_10w End ===

== Test stgu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetUserRank
排行榜用户数: 100000
总耗时: 42 ms
平均耗时: 0.42 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/12 20:27:12
√ 所有操作结果验证通过！
总耗时: 42 ms vs 27 ms (+55.56%)
平均耗时: 0.42 ms/1k操作 vs 0.27 ms/1k操作 (+55.56%)
内存占用: 2.29 MB vs 2.29 MB (+0.02%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stgu10w_10w End ===

== Test stuu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: UpdateUser
排行榜用户数: 100000
总耗时: 82 ms
平均耗时: 0.82 ms/1000操作
内存占用: 2.29 MB
内存峰值: 15.33 MB
测试日期: 2026/3/12 20:27:12
√ 所有操作结果验证通过！
总耗时: 82 ms vs 59 ms (+38.98%)
平均耗时: 0.82 ms/1k操作 vs 0.59 ms/1k操作 (+38.98%)
内存占用: 2.29 MB vs 2.29 MB (0.00%)
内存峰值: 15.33 MB vs 2.30 MB (+565.26%)
== Test stuu10w_10w End ===

== Test t100w_100w ===
用户数: 1000000
操作数: 1000000
排行榜用户数: 1099921
总耗时: 685 ms
平均耗时: 0.69 ms/1000操作
内存占用: 251.71 MB
内存峰值: 253.99 MB
测试日期: 2026/3/12 20:27:14
√ 所有操作结果验证通过！
总耗时: 685 ms vs 544 ms (+25.92%)
平均耗时: 0.69 ms/1k操作 vs 0.54 ms/1k操作 (+25.92%)
内存占用: 251.71 MB vs 251.84 MB (-0.05%)
内存峰值: 253.99 MB vs 251.83 MB (+0.86%)
== Test t100w_100w End ===

== Test t10w_10w ===
用户数: 100000
操作数: 100000
排行榜用户数: 109905
总耗时: 28 ms
平均耗时: 0.28 ms/1000操作
内存占用: 29.07 MB
内存峰值: 34.64 MB
测试日期: 2026/3/12 20:27:20
√ 所有操作结果验证通过！
总耗时: 28 ms vs 23 ms (+21.74%)
平均耗时: 0.28 ms/1k操作 vs 0.23 ms/1k操作 (+21.74%)
内存占用: 29.07 MB vs 29.07 MB (0.00%)
内存峰值: 34.64 MB vs 32.83 MB (+5.51%)
== Test t10w_10w End ===
*/
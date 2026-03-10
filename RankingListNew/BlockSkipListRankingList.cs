using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RankingListNew
{
    public class BlockSkipListRankingList : IRankingList
    {
        private static readonly int MaxLevel = 16; // 跳表的最大层数
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
            private Random _random = new();
            public int Count;
            private int _level;

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
                    currentLevelNodes[i] = Head;
                }
                foreach (var block in blocks)
                {
                    int randomLevel = RandomLevel();
                    SkipListNode newNode = new(block, randomLevel)
                    {
                        Previous = currentLevelNodes[0]
                    };
                    for (int i = 0; i < randomLevel; i++)
                    {
                        currentLevelNodes[i].Next[i] = newNode;
                        currentLevelNodes[i] = newNode;
                        newNode.PreviousCount[i] = userCount[i];
                        userCount[i] = 0;
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
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) < 0)
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

                int count, userIndexInBlock;
                UserBlock userBlock = current.UserBlock;
                if (!userBlock.Full)
                {
                    count = userBlock.UserCount;
                    userIndexInBlock = userBlock.Insert(user);
                    count += userCount.Sum();
                }
                else
                {
                    UserBlock newBlock = userBlock.Split(user, out userIndexInBlock);

                    int randomLevel = RandomLevel();
                    if (randomLevel > _level)
                    {
                        for (int i = _level - 1; i < randomLevel; i++)
                        {
                            update[i] = Head;
                        }
                        _level = randomLevel;
                    }
                    SkipListNode newNode = new(newBlock, randomLevel)
                    {
                        Previous = current
                    };
                    count = userBlock.UserCount;
                    for (int i = 0; i < randomLevel; i++)
                    {
                        newNode.Next[i] = update[i].Next[i];
                        update[i].Next[i] = newNode;
                        newNode.PreviousCount[i] = count;
                        newNode.Next[i]?.PreviousCount[i] -= count;
                        count += userCount[i];
                    }
                }

                Count++;

                return count + userIndexInBlock;
            }

            public void RemoveUser(User user)
            {
                int[] userCount = new int[MaxLevel];
                SkipListNode[] update = new SkipListNode[MaxLevel];
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) < 0)
                    {
                        current = current.Next[i];
                        userCount[i] += current.PreviousCount[i];
                    }
                    update[i] = current;
                    // 减少区间用户数量
                    if (current.Next[i] != null)
                    {
                        current.Next[i].PreviousCount[i]--;
                    }
                }
                Debug.Assert(current.Next[0] != null
                    && current.Next[0].UserBlock.MaxUser.CompareTo(user) >= 0, "用户不存在");


                UserBlock userBlock = current.Next[0].UserBlock;
                userBlock.Remove(user);
                bool needDelete = false;
                if (userBlock.Empty)
                {
                    needDelete = true;
                }
                else if (current.UserBlock.UserCount < BlockSize / 4
                    && current.Next[0].UserBlock.UserCount < BlockSize / 4)
                {
                    current.UserBlock.Combine(current.Next[0].UserBlock);
                    needDelete = true;
                }
                if (needDelete)
                {
                    if (current.Next[0].Next[0] != null)
                    {
                        current.Next[0].Next[0].Previous = current;
                    }
                    current = current.Next[0];
                    for (int i = 0; i < _level; i++)
                    {
                        if (update[i].Next[i] != current)
                        {
                            // 该层不包含current节点
                            break;
                        }
                        update[i].Next[i] = current.Next[i];
                        if (current.Next[i] != null)
                        {
                            current.Next[i].PreviousCount[i] += current.PreviousCount[i];
                        }
                    }
                    while (_level > 1 && Head.Next[_level] == null)
                    {
                        _level--;
                    }
                }
                Count--;
            }

            public int GetUserRank(User user)
            {
                int userCount = 0;
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) < 0)
                    {
                        current = current.Next[i];
                        userCount += current.PreviousCount[i];
                    }
                }
                current = current.Next[0];
                userCount += current.PreviousCount[0];
                Debug.Assert(current != null
                    && current.UserBlock.MaxUser.CompareTo(user) >= 0, "用户不存在");
                UserBlock userBlock = current.UserBlock;
                int userIndexInBlock = userBlock.IndexOf(user);
                Debug.Assert(userIndexInBlock >= 0, "用户不存在");
                return userCount + userIndexInBlock;


            }

            public User[] GetTopN(int topN)
            {
                topN = Math.Min(topN, Count);
                User[] result = new User[topN];
                SkipListNode? current = Head;
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
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) < 0)
                    {
                        current = current.Next[i];
                        rankCount += current.PreviousCount[i];
                    }
                }
                current = current.Next[0];
                UserBlock userBlock = current.UserBlock;
                int userIndexInBlock = userBlock.IndexOf(user);
                Debug.Assert(userIndexInBlock >= 0, "用户不存在");
                rankCount += current.PreviousCount[0] + userIndexInBlock;

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
                SkipListNode tNode = current;
                while (leftCount < leftNum)
                {
                    userBlock = tNode.UserBlock!;
                    int n = Math.Min(userBlock.UserCount, leftNum - leftCount);
                    Array.Copy(userBlock.Users, userBlock.UserCount - n, result, aroundN - leftCount - n + offset, n);
                    leftCount += n;
                }
                tNode = current;
                while (rightCount < rightNum)
                {
                    userBlock = tNode.Next[0]!.UserBlock!;
                    int n = Math.Min(userBlock.UserCount, rightNum - rightCount);
                    Array.Copy(userBlock.Users, 0, result, aroundN + rightCount + 1 + offset, n);
                    rightCount += n;
                }
                return (result, rankCount);
            }
#if DEBUG
            public void DebugPrint()
            {
                int[] levelCount = new int[MaxLevel];
                SkipListNode current = Head;
                while(current != null)
                {
                    levelCount[current.Next.Length - 1]++;
                    current = current.Next[0];
                }
                Console.WriteLine($"总用户数：{Count}");
                for(int i = 0; i < MaxLevel; i++)
                {
                    Console.WriteLine($"Level {i + 1}: {levelCount[i]}");
                }
            }
#endif
        }

        class SkipListNode
        {
            public UserBlock UserBlock;
            public SkipListNode?[] Next;
            public SkipListNode? Previous;
            // 每一层到前一个节点的用户数量（不包含本节点的用户数量）
            public int[] PreviousCount;
            public User MinUser => UserBlock.MinUser;

            public SkipListNode(UserBlock block, int level)
            {
                UserBlock = block;
                Next = new SkipListNode[level];
                PreviousCount = new int[level];
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

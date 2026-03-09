using System.Diagnostics;

namespace RankingListNew
{
    public class BucketSkipListRankingList : IRankingList
    {
        private static readonly int MaxLevel = 16; // 跳表的最大层数
        private static readonly double Probability = 0.5; // 跳表的概率
        private static readonly int BlockSize = 256; // 每个block的用户数量
        private static readonly int InitialBlockSize = BlockSize / 2; // 初始每个block的用户数量

        // 参考：https://cloud.tencent.com/developer/article/2512982
        class SkipList
        {
            public SkipListNode Head;
            public int Count;
            private int _level;

            private int RandomLevel()
            {
                int level = 1;
                while (new Random().NextDouble() < Probability && level < MaxLevel)
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
                for (int i = _level; i >= 0; i--)
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
                if (userBlock.Full)
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
                    count = userBlock.UserCount;
                    for (int i = 0; i < randomLevel; i++)
                    {
                        newNode.Next[i] = update[i].Next[i];
                        update[i].Next[i] = newNode;
                        newNode.PreviousCount[i] = count;
                        if (newNode.Next[i] != null)
                        {
                            newNode.Next[i].PreviousCount[i] -= count;
                        }
                        count += userCount[i];
                    }
                }
                else
                {
                    userIndexInBlock = userBlock.Insert(user);
                    count = userCount.Sum();
                }

                Count++;

                return count + userIndexInBlock;
            }

            public void RemoveUser(User user)
            {
                int[] userCount = new int[MaxLevel];
                SkipListNode[] update = new SkipListNode[MaxLevel];
                SkipListNode current = Head;
                for (int i = _level; i >= 0; i--)
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
                bool needMerge = false;
                if(userBlock.Empty)
                {
                    needMerge = true;
                }
                else if(current.Next[0] != null && userBlock.UserCount < BlockSize / 4 
                    && current.Next[0].UserBlock.UserCount < BlockSize / 4)
                {
                    current.UserBlock.Combine(current.Next[0].UserBlock);
                    current = current.Next[0];
                    needMerge = true;
                }
                if (needMerge)
                {
                    for (int i = 0; i < _level; i++)
                    {
                        update[i].Next[i] = current.Next[i];
                        if (current.Next[i] != null)
                        {
                            current.Next[i].PreviousCount[i] += current.PreviousCount[i];
                        }
                    }
                    while (_level > 0 && Head.Next[_level] == null)
                    {
                        _level--;
                    }
                }
                Count--;
            }

            public int GetUserRank(User user)
            {
                int rank = 0;
                SkipListNode current = Head;
                for (int i = _level - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].MinUser.CompareTo(user) < 0)
                    {
                        rank += current.PreviousCount[i];
                        current = current.Next[i];
                    }
                }
                return rank;
            }

            public User[] GetTopN(int n)
            {
                User[] result = new User[n];
                SkipListNode current = Head.Next[0];
                while (current != null && result.Count < n)
                {
                    for (int i = 0; i < current.UserBlock.UserCount && result.Count < n; i++)
                    {
                        result.Add(current.UserBlock.Users[i]);
                    }
                    current = current.Next[0];
                }
                return result.ToArray();
            }
        }

        class SkipListNode
        {
            public UserBlock UserBlock;
            public SkipListNode[] Next;
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

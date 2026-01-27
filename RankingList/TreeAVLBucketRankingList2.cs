using System.Diagnostics;

namespace RankingList
{
    public class TreeAVLBucketRankingList2 : IRankingList
    {
        private static readonly int BucketSize = 256; // 每个bucket的用户数量
        private static readonly int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量
        private TreeNode _root;
        private Dictionary<int, IUser> _userMap;

        public TreeAVLBucketRankingList2(IUser[] users)
        {
            Array.Sort(users);
            UserBucket[] buckets = BuildBucket(users);
            _root = BuildTree(0, buckets.Length, buckets);
            _userMap = users.ToDictionary(u => u.Id, u => u);
        }

        private static UserBucket[] BuildBucket(IUser[] users)
        {
            // 初始化bucket
            int bucketNum = (int)Math.Ceiling((double)users.Length / InitialBucketSize);
            UserBucket[] buckets = new UserBucket[bucketNum];
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * InitialBucketSize;
                int r = Math.Min((i + 1) * InitialBucketSize, users.Length);
                int userCount = r - l;
                IUser[] bucketUsers = new IUser[BucketSize];
                Array.Copy(users, l, bucketUsers, 0, userCount);

                buckets[i] = new UserBucket(bucketUsers, userCount);
            }

            return buckets;
        }

        private static TreeNode BuildTree(int l, int r, UserBucket[] buckets)
        {
            // 初始化tree
            TreeNode node = new();
            if (l + 1 == r)
            {
                node.Count = buckets[l].UserCount;
                node.UserBucket = buckets[l];
                node.LeftUser = buckets[l].MinUser;
                node.RightUser = buckets[l].MaxUser;
                return node;
            }

            int mid = (l + r) >> 1;
            node.Left = BuildTree(l, mid, buckets);
            node.LeftUser = node.Left.LeftUser;
            node.Right = BuildTree(mid, r, buckets);
            node.RightUser = node.Right.RightUser;
            node.Count = node.Left.Count + node.Right.Count;
            return node;
        }

        private static TreeNode OperateTree(TreeNode node, IUser user, ref int rankCount, bool isAdd)
        {
            // 叶子节点
            if (node.UserBucket != null)
            {
                if (isAdd)
                {
                    int userIndexInBucket;
                    if (node.Full)
                    {
                        // 分裂TreeNode
                        node.Split(user, out userIndexInBucket);
                    }
                    else
                    {
                        // 加入bucket
                        userIndexInBucket = node.Insert(user);
                    }

                    rankCount += userIndexInBucket;
                }
                else
                {
                    node.Remove(user);
                }

                return node;
            }

            // 非叶子节点，必定度为2
            Debug.Assert(node.Left != null && node.Right != null);
            if (user.CompareTo(node.Right.LeftUser) < 0)
            {
                node.Left = OperateTree(node.Left, user, ref rankCount, isAdd);
                node.LeftUser = node.Left.LeftUser;
            }
            else
            {
                rankCount += node.Left.Count;
                node.Right = OperateTree(node.Right, user, ref rankCount, isAdd);
                node.RightUser = node.Right.RightUser;
            }

            if (isAdd)
            {
                node.Count++;
            }
            else
            {
                node.Count--;
            }

            Debug.Assert(node.Count == node.Left.Count + node.Right.Count);
            if (node.Left.Empty)
            {
                // 左子树为空，用右子树代替
                node.CopyFrom(node.Right);
            }
            else if (node.Right.Empty)
            {
                // 右子树为空，用左子树代替
                node.CopyFrom(node.Left);
            }
            else if (node.Count < BucketSize / 4)
            {
                // 合并TreeNode
                node.CombineChild();
            }
            else
            {
                switch (node.Left.Height - node.Right.Height)
                {
                    // 平衡二叉树
                    case > 1:
                        // 左子树高度大于右子树高度，需要右旋
                        if (node.Left.Right.Height > node.Left.Left.Height)
                        {
                            // 左子树的右子树高度大于左子树高度，需要先左旋
                            node.Left = TreeNode.RotateLeft(node.Left);
                        }

                        node = TreeNode.RotateRight(node);
                        break;
                    case < -1:
                        // 右子树高度大于左子树高度，需要左旋
                        if (node.Right.Left.Height > node.Right.Right.Height)
                        {
                            // 右子树的左子树高度大于右子树高度，需要先右旋
                            node.Right = TreeNode.RotateRight(node.Right);
                        }

                        node = TreeNode.RotateLeft(node);
                        break;
                    default:
                        node.Height = Math.Max(node.Left.Height, node.Right.Height) + 1;
                        break;
                }
            }
            return node;
        }

        public RankingListResponse AddUser(IUser user)
        {
            Debug.Assert(!_userMap.ContainsKey(user.Id));
            _userMap.Add(user.Id, user);
            int rankCount = 0;
            _root = OperateTree(_root, user, ref rankCount, true);
            return new RankingListResponse
            {
                User = user,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse UpdateUser(IUser user)
        {
            int rankCount = 0;
            IUser oldUser = _userMap[user.Id];
            _root = OperateTree(_root, oldUser, ref rankCount, false);
            rankCount = 0;
            _root = OperateTree(_root, user, ref rankCount, true);
            _userMap[user.Id] = user;
            return new RankingListResponse
            {
                User = user,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse GetUserRank(int userId)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            IUser user = _userMap[userId];
            int rankCount = 0;
            TreeNode node = _root;

            while (node.UserBucket == null)
            {
                Debug.Assert(node.Left != null && node.Right != null);
                if (user.CompareTo(node.Left.RightUser) <= 0)
                {
                    node = node.Left;
                }
                else
                {
                    rankCount += node.Left.Count;
                    node = node.Right;
                }
            }

            UserBucket bucket = node.UserBucket;
            int userIndexInBucket = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, user);
            Debug.Assert(userIndexInBucket >= 0);
            rankCount += userIndexInBucket;
            return new RankingListResponse
            {
                User = user,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            RankingListResponse[] result = new RankingListResponse[topN];
            int rankCount = 0;
            GetTopN(_root, topN, ref rankCount, ref result);
            return result;
        }

        private static void GetTopN(TreeNode node, int topN, ref int rankCount, ref RankingListResponse[] result)
        {
            if (node.UserBucket != null)
            {
                for (int i = 0; i < node.UserBucket.UserCount && rankCount < topN; i++, rankCount++)
                {
                    result[rankCount] = new RankingListResponse
                    {
                        User = node.UserBucket.Users[i],
                        Rank = rankCount + 1
                    };
                }

                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            GetTopN(node.Left, topN, ref rankCount, ref result);
            if (rankCount < topN)
            {
                GetTopN(node.Right, topN, ref rankCount, ref result);
            }
        }

        // 先获取用户在树中的排名，再获取左右aroundN个用户
        private static void GetAroundUserStep1(TreeNode node, IUser user, int aroundN, ref int rankCount,
            ref int leftCount, ref int rightCount, ref RankingListResponse[] result)
        {
            if (node.UserBucket != null)
            {
                UserBucket bucket = node.UserBucket;
                int userIndexInBucket = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, user);
                Debug.Assert(userIndexInBucket >= 0);
                rankCount += userIndexInBucket;
                result[aroundN] = new RankingListResponse
                {
                    User = bucket.Users[userIndexInBucket],
                    Rank = rankCount + 1
                };
                // 左边
                for (int i = userIndexInBucket - 1; i >= 0 && leftCount < aroundN; i--, leftCount++)
                {
                    result[aroundN - leftCount - 1] = new RankingListResponse
                    {
                        User = bucket.Users[i],
                        Rank = rankCount - (leftCount + 1) + 1
                    };
                }

                // 右边
                for (int i = userIndexInBucket + 1; i < bucket.UserCount && rightCount < aroundN; i++, rightCount++)
                {
                    result[aroundN + rightCount + 1] = new RankingListResponse
                    {
                        User = bucket.Users[i],
                        Rank = rankCount + (rightCount + 1) + 1
                    };
                }

                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            if (user.CompareTo(node.Right.LeftUser) < 0)
            {
                GetAroundUserStep1(node.Left, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
                // 找到用户后，进入第二阶段
                if (rightCount < aroundN)
                {
                    GetAroundUserStep2(node.Right, aroundN, false, rankCount, ref rightCount, ref result);
                }
            }
            else
            {
                rankCount += node.Left.Count;
                GetAroundUserStep1(node.Right, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
                // 找到用户后，进入第二阶段
                if (leftCount < aroundN)
                {
                    GetAroundUserStep2(node.Left, aroundN, true, rankCount, ref leftCount, ref result);
                }
            }
        }

        private static void GetAroundUserStep2(TreeNode node, int aroundN, bool isRequiredLeft, int rankCount,
            ref int requiredCount, ref RankingListResponse[] result)
        {
            if (node.UserBucket != null)
            {
                UserBucket bucket = node.UserBucket;
                if (isRequiredLeft)
                {
                    // 缺少左边的用户
                    for (int i = bucket.UserCount - 1; i >= 0 && requiredCount < aroundN; i--, requiredCount++)
                    {
                        result[aroundN - requiredCount - 1] = new RankingListResponse
                        {
                            User = bucket.Users[i],
                            Rank = rankCount - (requiredCount + 1) + 1
                        };
                    }
                }
                else
                {
                    // 缺少右边的用户
                    for (int i = 0; i < bucket.UserCount && requiredCount < aroundN; i++, requiredCount++)
                    {
                        result[aroundN + requiredCount + 1] = new RankingListResponse
                        {
                            User = bucket.Users[i],
                            Rank = rankCount + (requiredCount + 1) + 1
                        };
                    }
                }

                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            TreeNode[] children = isRequiredLeft ? [node.Right, node.Left] : [node.Left, node.Right];
            foreach (TreeNode child in children)
            {
                GetAroundUserStep2(child, aroundN, isRequiredLeft, rankCount, ref requiredCount, ref result);
                if (requiredCount >= aroundN)
                {
                    break;
                }
            }
        }

#if DEBUG
        public void DebugPrint()
        {
            List<(int depth, int count)> results = [];
            DebugPrint(_root, 0, ref results);
            for (int i = 0; i < results.Count; i++)
            {
                Console.Write($"{results[i].depth}-{results[i].count}  ");
                // 每10个换行
                if ((i + 1) % 10 == 0)
                {
                    Console.WriteLine();
                }
            }
        }

        private void DebugPrint(TreeNode node, int depth, ref List<(int depth, int count)> results)
        {
            if (node.UserBucket != null)
            {
                results.Add((depth, node.UserBucket.UserCount));
                return;
            }

            DebugPrint(node.Left, depth + 1, ref results);
            DebugPrint(node.Right, depth + 1, ref results);
        }
#endif

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            IUser user = _userMap[userId];
            int rankCount = 0;
            int leftCount = 0;
            int rightCount = 0;
            RankingListResponse[] result = new RankingListResponse[aroundN * 2 + 1];
            GetAroundUserStep1(_root, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
            if (leftCount < aroundN || rightCount < aroundN)
            {
                // 未填满
                RankingListResponse[] newResult = new RankingListResponse[leftCount + rightCount + 1];
                Array.Copy(result, aroundN - leftCount, newResult, 0, leftCount + rightCount + 1);
                result = newResult;
            }

            return result;
        }

        public int GetRankingCount()
        {
            return _root.Count;
        }

        /// <summary>
        /// 每个桶
        /// </summary>
        class UserBucket
        {
            public IUser MinUser => Users[0];
            public IUser MaxUser => Users[UserCount - 1];

            public IUser[] Users { get; }
            public int UserCount { get; private set; }
            public bool Full => UserCount >= Users.Length;
            public bool Empty => UserCount == 0;

            public UserBucket(IUser[] users, int userCount)
            {
                Users = users;
                UserCount = userCount;
            }

            public int Insert(IUser user)
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

            public int Remove(IUser user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                Debug.Assert(index >= 0);

                Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
                Users[UserCount - 1] = null;
                UserCount--;
                return index;
            }

            /// <summary>
            /// 分裂成两个桶
            /// </summary>
            /// <param name="user"></param>
            /// <param name="userIndex"></param>
            /// <returns>右边的新桶</returns>
            public UserBucket Split(IUser user, out int userIndex)
            {
                int mid = UserCount / 2;
                userIndex = Array.BinarySearch(Users, 0, UserCount, user);
                if (userIndex < 0)
                {
                    userIndex = ~userIndex;
                }

                IUser[] newUsers = new IUser[BucketSize];
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

                Array.Clear(Users, mid, UserCount - mid);

                UserCount = mid;
                UserBucket newBucket = new(newUsers, newUserCount);
                if (userIndex < mid)
                    Insert(user);
                return newBucket;
            }

            public void Combine(UserBucket other)
            {
                Debug.Assert(UserCount + other.UserCount <= Users.Length);
                Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
                UserCount += other.UserCount;
            }
        }

        class TreeNode
        {
            public int Count;
            public IUser LeftUser;
            public IUser RightUser;
            public TreeNode? Left;
            public TreeNode? Right;
            public UserBucket? UserBucket;
            public bool Full => Count >= BucketSize;
            public bool Empty => Count == 0;
            public int Height;

            public void CopyFrom(TreeNode other)
            {
                Count = other.Count;
                LeftUser = other.LeftUser;
                RightUser = other.RightUser;
                Left = other.Left;
                Right = other.Right;
                UserBucket = other.UserBucket;
                Height = other.Height;
            }

            public int Insert(IUser user)
            {
                Debug.Assert(UserBucket != null);
                int userIndexInBucket = UserBucket.Insert(user);
                if (userIndexInBucket == 0)
                {
                    LeftUser = user;
                }
                else if (userIndexInBucket == UserBucket.UserCount - 1)
                {
                    RightUser = user;
                }

                Count++;
                return userIndexInBucket;
            }

            public void Remove(IUser user)
            {
                Debug.Assert(UserBucket != null);
                int userIndexInBucket = UserBucket.Remove(user);
                if (UserBucket.Empty)
                {
                    // LeftUser = null;
                    // RightUser = null;
                }
                else if (userIndexInBucket == 0)
                {
                    LeftUser = UserBucket.MinUser;
                }
                else if (userIndexInBucket == UserBucket.UserCount)
                {
                    RightUser = UserBucket.MaxUser;
                }

                Count--;
            }

            public void Split(IUser user, out int userIndexInBucket)
            {
                Debug.Assert(UserBucket != null);
                UserBucket newBucket = UserBucket.Split(user, out userIndexInBucket);
                Left = new TreeNode()
                {
                    UserBucket = UserBucket,
                    Count = UserBucket.UserCount,
                    LeftUser = UserBucket.MinUser,
                    RightUser = UserBucket.MaxUser
                };
                Right = new TreeNode()
                {
                    UserBucket = newBucket,
                    Count = newBucket.UserCount,
                    LeftUser = newBucket.MinUser,
                    RightUser = newBucket.MaxUser
                };
                UserBucket = null;
                Count = Left.Count + Right.Count;
                Height = 1;
            }

            public void CombineChild()
            {
                Debug.Assert(Left != null && Right != null);
                if (Left.UserBucket == null)
                {
                    Left.CombineChild();
                }

                if (Right.UserBucket == null)
                {
                    Right.CombineChild();
                }

                Debug.Assert(Left.UserBucket != null && Right.UserBucket != null);
                UserBucket = Left.UserBucket;
                UserBucket.Combine(Right.UserBucket);
                Debug.Assert(UserBucket.UserCount == Count);
                Debug.Assert(UserBucket.MinUser == LeftUser);
                Debug.Assert(UserBucket.MaxUser == RightUser);
                Left = null;
                Right = null;
                Height = 0;
            }

            public static TreeNode RotateLeft(TreeNode x)
            {
                Debug.Assert(x.Right != null && x.Left != null &&
                             x.Right.Left != null && x.Right.Right != null);
                TreeNode y = x.Right;
                x.Right = y.Left;
                y.Left = x;
                x.Height = Math.Max(x.Left.Height, x.Right.Height) + 1;
                y.Height = Math.Max(y.Left.Height, y.Right.Height) + 1;
                x.RightUser = x.Right.RightUser;
                y.LeftUser = x.LeftUser;
                x.Count = x.Left.Count + x.Right.Count;
                y.Count = y.Left.Count + y.Right.Count;
                return y;
            }

            public static TreeNode RotateRight(TreeNode x)
            {
                Debug.Assert(x.Left != null && x.Left.Left != null &&
                             x.Left.Right != null && x.Right != null);
                TreeNode y = x.Left;
                x.Left = y.Right;
                y.Right = x;
                x.Height = Math.Max(x.Left.Height, x.Right.Height) + 1;
                y.Height = Math.Max(y.Left.Height, y.Right.Height) + 1;
                x.LeftUser = x.Left.LeftUser;
                y.RightUser = x.RightUser;
                x.Count = x.Left.Count + x.Right.Count;
                y.Count = y.Left.Count + y.Right.Count;
                return y;
            }
        }
    }
}
/*
=== 排行榜测试框架 ===

=== 测试 TreeAVLBucketRankingList2 排行榜 ===
初始用户数: 10000
操作数: 1000000

=== 验证操作结果与基准对比 ===
√ 所有操作结果验证通过！
测试操作结果已保存到 TreeAVLBucketRankingList2_test_results.json

=== 测试结果 ===
排行榜名称: TreeAVLBucketRankingList2
总耗时: 1808 ms
平均耗时: 1.81 ms/1000操作
内存占用: 709.57 MB
内存峰值: 709.57 MB
测试日期: 2026/1/27 14:12:14

=== 与基准 BucketRankingList 的对比 ===
总耗时: 1808 ms vs 3122 ms (-42.09%)
平均耗时: 1.81 ms/1000操作 vs 3.12 ms/1000操作 (-42.09%)
内存占用: 709.57 MB vs 707.04 MB (+0.36%)
内存峰值: 709.57 MB vs 707.04 MB (+0.36%)

=== 单项操作耗时测试 ===

【AddUser】
  操作数: 100000 vs 100000
  总耗时: 51 ms vs 190 ms (-73.16%) (100000次操作)
  平均耗时: 0.51 ms/1000操作 vs 1.90 ms/1000操作 (-73.16%)
【UpdateUser】
  操作数: 200000 vs 200000
  总耗时: 344 ms vs 2874 ms (-88.03%) (200000次操作)
  平均耗时: 1.72 ms/1000操作 vs 14.37 ms/1000操作 (-88.03%)
【GetUserRank】
  操作数: 300000 vs 300000
  总耗时: 242 ms vs 2313 ms (-89.54%) (300000次操作)
  平均耗时: 0.81 ms/1000操作 vs 7.71 ms/1000操作 (-89.54%)
【GetTopN】
  操作数: 200000 vs 200000
  总耗时: 471 ms vs 608 ms (-22.53%) (200000次操作)
  平均耗时: 2.36 ms/1000操作 vs 3.04 ms/1000操作 (-22.53%)
【GetAroundUser】
  操作数: 200000 vs 200000
  总耗时: 298 ms vs 1765 ms (-83.12%) (200000次操作)
  平均耗时: 1.49 ms/1000操作 vs 8.83 ms/1000操作 (-83.12%)
*/
// 属性确实比字段慢
using System.Diagnostics;

namespace RankingList
{
    public class TreeBRTreeBucketRankingListST : IRankingList
    {
        private static readonly int BucketSize = 128; // 每个bucket的用户数量
        private static readonly int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量
        private TreeNode _root;
        private Dictionary<int, UserValue> _userMap;
        private const int TopCount = 100; // 顶部用户数量
        private UserBucket _topBucket;

        public TreeBRTreeBucketRankingListST(IUser[] users)
        {
            UserValue[] userValues = users.Select(u => new UserValue((User)u)).ToArray();
            Array.Sort(userValues);
            UserBucket[] buckets = BuildBucket(userValues);
            _root = BuildTree(0, buckets.Length, 1, buckets);
            _root.Color = ColorEnum.Black;
            _userMap = userValues.ToDictionary(u => u.Id, u => u);

#if DEBUG
            CheckTree();
#endif
        }

        private UserBucket[] BuildBucket(UserValue[] users)
        {
            // 初始化topBucket
            int topUserCount = Math.Min(TopCount, users.Length);
            UserValue[] topUserValues = new UserValue[TopCount];
            Array.Copy(users, 0, topUserValues, 0, topUserCount);
            _topBucket = new UserBucket(topUserValues, topUserCount);

            // 初始化bucket
            int bucketNum = (int)Math.Ceiling((double)(users.Length - topUserCount) / InitialBucketSize);
            UserBucket[] buckets = new UserBucket[bucketNum];
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * InitialBucketSize + topUserCount;
                int r = Math.Min((i + 1) * InitialBucketSize + topUserCount, users.Length);
                int userCount = r - l;
                UserValue[] bucketUsers = new UserValue[BucketSize];
                Array.Copy(users, l, bucketUsers, 0, userCount);
                buckets[i] = new UserBucket(bucketUsers, userCount);
            }

            return buckets;
        }

        private static TreeNode BuildTree(int l, int r, int depth, UserBucket[] buckets)
        {
            int maxDepth = (int)Math.Ceiling(Math.Log(buckets.Length - 1, 2)) + 1;
            // 初始化tree
            TreeNode node = new()
            {
                Color = (maxDepth - depth) % 2 == 0 ? ColorEnum.Red : ColorEnum.Black
            };
            if (l + 1 == r)
            {
                node.Count = buckets[l].UserCount;
                node.UserBucket = buckets[l];
                node.LeftUser = buckets[l].MinUser;
                node.RightUser = buckets[l].MaxUser;
                return node;
            }

            int mid = (l + r) >> 1;
            node.Left = BuildTree(l, mid, depth + 1, buckets);
            node.Left.Parent = node;
            node.LeftUser = node.Left.LeftUser;
            node.Right = BuildTree(mid, r, depth + 1, buckets);
            node.Right.Parent = node;
            node.RightUser = node.Right.RightUser;
            node.Count = node.Left.Count + node.Right.Count;
            return node;
        }

#if DEBUG
        public void CheckTree()
        {
            Debug.Assert(_root.Color == ColorEnum.Black);
            CheckTree(_root);
        }

        private static int CheckTree(TreeNode? node)
        {
            if (node == null)
            {
                return 1;
            }

            int leftBlackCount = CheckTree(node.Left);
            Debug.Assert(node.Left == null || node.Left.Parent == node);
            int rightBlackCount = CheckTree(node.Right);
            Debug.Assert(node.Right == null || node.Right.Parent == node);
            Debug.Assert(node.Left == null || node.Right == null || node.Left.Count + node.Right.Count == node.Count);
            Debug.Assert(node.UserBucket == null || node.UserBucket.UserCount == node.Count);
            Debug.Assert(node.Left == null || node.LeftUser.CompareTo(node.Left.LeftUser) == 0);
            Debug.Assert(node.Right == null || node.RightUser.CompareTo(node.Right.RightUser) == 0);
            if (node.Color == ColorEnum.Red)
            {
                Debug.Assert(node.Left == null || node.Left.Color == ColorEnum.Black);
                Debug.Assert(node.Right == null || node.Right.Color == ColorEnum.Black);
            }

            Debug.Assert(leftBlackCount == rightBlackCount,
                $"leftBlackCount: {leftBlackCount}, rightBlackCount: {rightBlackCount}");
            return node.Color == ColorEnum.Black ? leftBlackCount + 1 : leftBlackCount;
        }
#endif
        // 参考：https://www.cnblogs.com/crazymakercircle/p/16320430.html
        // 参考：https://blog.csdn.net/u014454538/article/details/120120216
        private int AddTreeUser(UserValue user)
        {
            int rankCount = 0;
            TreeNode node = _root;
            while (node.UserBucket == null)
            {
                node.Count++;
                if (user.CompareTo(node.Right!.LeftUser) < 0)
                {
                    node = node.Left!;
                }
                else
                {
                    rankCount += node.Left!.Count;
                    node = node.Right!;
                }
            }

            // 叶子节点
            int userIndexInBucket;
            if (node.Full)
            {
                // 分裂TreeNode
                node.Split(user, out userIndexInBucket);
                rankCount += userIndexInBucket;
                // 调节树
                if (node.Color == ColorEnum.Red)
                {
                    // 红色必定不是根节点
                    TreeNode parentNode = node.Parent!;
                    TreeNode siblingNode = parentNode.Left == node
                        ? parentNode.Right!
                        : parentNode.Left!;
                    // 兄弟必定为红色
                    Debug.Assert(siblingNode.Color == ColorEnum.Red);
                    node.Color = ColorEnum.Black;
                    siblingNode.Color = ColorEnum.Black;
                    parentNode.Color = ColorEnum.Red;
                    FixAfterAdd(parentNode);
                }
#if DEBUG
                CheckTree();
#endif
            }
            else
            {
                // 加入bucket
                userIndexInBucket = node.Insert(user);
                rankCount += userIndexInBucket;
            }

            return rankCount;
        }

        private void FixAfterAdd(TreeNode node)
        {
            while (node != _root && node.Parent!.Color == ColorEnum.Red)
            {
                TreeNode parentNode = node.Parent!;
                // 父亲为红
                TreeNode grandParentNode = parentNode.Parent!;
                TreeNode uncleNode = grandParentNode.Left == parentNode
                    ? grandParentNode.Right!
                    : grandParentNode.Left!;
                if (uncleNode.Color == ColorEnum.Red)
                {
                    // 叔叔为红
                    parentNode.Color = ColorEnum.Black;
                    uncleNode.Color = ColorEnum.Black;
                    grandParentNode.Color = ColorEnum.Red;
                    node = grandParentNode;
                }
                else
                {
                    // 叔叔为黑
                    if (parentNode == grandParentNode.Left)
                    {
                        if (node == parentNode.Right)
                        {
                            // 左旋转
                            parentNode = RotateLeft(parentNode);
                            // node不需要多余赋值
                        }

                        // 变色
                        parentNode.Color = ColorEnum.Black;
                        grandParentNode.Color = ColorEnum.Red;
                        // 右旋转
                        RotateRight(grandParentNode);
                    }
                    else
                    {
                        if (node == parentNode.Left)
                        {
                            // 右旋转
                            parentNode = RotateRight(parentNode);
                        }

                        // 变色
                        parentNode.Color = ColorEnum.Black;
                        grandParentNode.Color = ColorEnum.Red;
                        // 左旋转
                        RotateLeft(grandParentNode);
                    }

                    break;
                }
            }

            _root.Color = ColorEnum.Black;
        }

        // 参考： https://zhuanlan.zhihu.com/p/91960960
        private void RemoveTreeUser(UserValue user)
        {
            TreeNode node = _root;
            while (node.UserBucket == null)
            {
                node.Count--;
                node = user.CompareTo(node.Right!.LeftUser) < 0 ? node.Left! : node.Right!;
            }

            // 叶子节点
            node.Remove(user);
            if (node == _root)
                return;

            TreeNode parent = node.Parent!;
            ColorEnum parentColor = parent.Color;
            TreeNode siblingNode = parent.Left == node ? parent.Right! : parent.Left!;
            ColorEnum siblingColor = siblingNode.Color;
            if (node.Empty)
            {
                parent.MoveFromChild(siblingNode);
                parent.Color = ColorEnum.Black;
                if (parentColor == ColorEnum.Black && siblingColor == ColorEnum.Black)
                {
                    // 合并以后就会少了一个黑，需要调整
                    FixAfterDel(parent);
                }
#if DEBUG
                CheckTree();
#endif
            }
            else if (siblingNode.UserBucket != null && parent.Count < BucketSize / 4)
            {
                parent.CombineChild();
                parent.Color = ColorEnum.Black;
                if (parentColor == ColorEnum.Black && siblingColor == ColorEnum.Black)
                {
                    // 合并以后就会少了一个黑，需要调整
                    FixAfterDel(parent);
                }
#if DEBUG
                CheckTree();
#endif
            }
        }

        private void FixAfterDel(TreeNode node)
        {
            while (node != _root && node.Color == ColorEnum.Black)
            {
                TreeNode parentNode = node.Parent!;
                if (node == parentNode.Left)
                {
                    TreeNode siblingNode = parentNode.Right!;
                    // 兄弟节点为红
                    if (siblingNode.Color == ColorEnum.Red)
                    {
                        // 变色
                        siblingNode.Color = ColorEnum.Black;
                        parentNode.Color = ColorEnum.Red;
                        // 左旋转
                        RotateLeft(parentNode);
                        siblingNode = parentNode.Right!;
                    }

                    // 兄弟节点为黑
                    if (siblingNode.Left!.Color == ColorEnum.Black && siblingNode.Right!.Color == ColorEnum.Black)
                    {
                        // 变色
                        siblingNode.Color = ColorEnum.Red;
                        node = parentNode;
                    }
                    else
                    {
                        if (siblingNode.Right!.Color == ColorEnum.Black)
                        {
                            // 变色
                            siblingNode.Left!.Color = ColorEnum.Black;
                            siblingNode.Color = ColorEnum.Red;
                            // 右旋转
                            siblingNode = RotateRight(siblingNode);
                        }

                        // 变色
                        siblingNode.Color = parentNode.Color;
                        parentNode.Color = ColorEnum.Black;
                        siblingNode.Right!.Color = ColorEnum.Black;
                        // 左旋转
                        RotateLeft(parentNode);
                        node = _root;
                    }
                }
                else
                {
                    TreeNode siblingNode = parentNode.Left!;
                    // 兄弟节点为红
                    if (siblingNode.Color == ColorEnum.Red)
                    {
                        // 变色
                        siblingNode.Color = ColorEnum.Black;
                        parentNode.Color = ColorEnum.Red;
                        // 右旋转
                        RotateRight(parentNode);
                        siblingNode = parentNode.Left!;
                    }

                    // 兄弟节点为黑
                    if (siblingNode.Left!.Color == ColorEnum.Black && siblingNode.Right!.Color == ColorEnum.Black)
                    {
                        // 变色
                        siblingNode.Color = ColorEnum.Red;
                        node = parentNode;
                    }
                    else
                    {
                        if (siblingNode.Left!.Color == ColorEnum.Black)
                        {
                            // 变色
                            siblingNode.Right!.Color = ColorEnum.Black;
                            siblingNode.Color = ColorEnum.Red;
                            // 左旋转
                            siblingNode = RotateLeft(siblingNode);
                        }

                        // 变色
                        siblingNode.Color = parentNode.Color;
                        parentNode.Color = ColorEnum.Black;
                        siblingNode.Left!.Color = ColorEnum.Black;
                        // 右旋转
                        RotateRight(parentNode);
                        node = _root;
                    }
                }
            }

            // 根节点
            node.Color = ColorEnum.Black;
        }

        private TreeNode RotateLeft(TreeNode x)
        {
            Debug.Assert(x.Right != null && x.Left != null &&
                         x.Right.Left != null && x.Right.Right != null);
            TreeNode y = x.Right;
            x.Right = y.Left;
            x.Right.Parent = x;
            y.Left = x;
            y.Parent = x.Parent;
            x.Parent = y;
            if (y.Parent != null)
            {
                if (x == y.Parent.Left)
                {
                    y.Parent.Left = y;
                }
                else if (x == y.Parent.Right)
                {
                    y.Parent.Right = y;
                }
                else
                {
                    Debug.Assert(false);
                }
            }

            x.RightUser = x.Right.RightUser;
            y.LeftUser = x.LeftUser;
            x.Count = x.Left.Count + x.Right.Count;
            y.Count = y.Left.Count + y.Right.Count;
            if (y.Parent == null)
                _root = y;
            return y;
        }

        private TreeNode RotateRight(TreeNode x)
        {
            Debug.Assert(x.Left != null && x.Left.Left != null &&
                         x.Left.Right != null && x.Right != null);
            TreeNode y = x.Left;
            x.Left = y.Right;
            x.Left.Parent = x;
            y.Right = x;
            y.Parent = x.Parent;
            x.Parent = y;
            if (y.Parent != null)
            {
                if (x == y.Parent.Left)
                {
                    y.Parent.Left = y;
                }
                else
                {
                    y.Parent.Right = y;
                }
            }

            x.LeftUser = x.Left.LeftUser;
            y.RightUser = x.RightUser;
            x.Count = x.Left.Count + x.Right.Count;
            y.Count = y.Left.Count + y.Right.Count;
            if (y.Parent == null)
                _root = y;
            return y;
        }

        private int AddUser(UserValue user)
        {
            int rankCount;
            if (!_topBucket.Full)
            {
                rankCount = _topBucket.Insert(user);
            }
            else if (user.CompareTo(_topBucket.MaxUser) < 0)
            {
                UserValue maxUser = _topBucket.MaxUser;
                _topBucket.Remove(maxUser);
                rankCount = _topBucket.Insert(user);
                AddTreeUser(maxUser);
            }
            else
            {
                rankCount = AddTreeUser(user) + _topBucket.UserCount;
            }

            return rankCount;
        }

        private void RemoveUser(UserValue user)
        {
            if (user.CompareTo(_topBucket.MaxUser) <= 0)
            {
                _topBucket.Remove(user);
            }
            else
            {
                RemoveTreeUser(user);
            }
        }

        public RankingListResponse AddUser(IUser user)
        {
            Debug.Assert(!_userMap.ContainsKey(user.Id));
            UserValue userValue = new((User)user);
            _userMap.Add(user.Id, userValue);
            int rankCount = AddUser(userValue);

            return new RankingListResponse
            {
                User = user,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse UpdateUser(IUser newUser)
        {
            UserValue oldUserValue = _userMap[newUser.Id];
            RemoveUser(oldUserValue);
            UserValue newUserValue = new((User)newUser);
            int rankCount = AddUser(newUserValue);
            _userMap[newUser.Id] = newUserValue;
            return new RankingListResponse
            {
                User = newUser,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse GetUserRank(int userId)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            UserValue userValue = _userMap[userId];
            int rankCount;
            if (userValue.CompareTo(_topBucket.MaxUser) <= 0)
            {
                rankCount = Array.BinarySearch(_topBucket.Users, 0, _topBucket.UserCount, userValue);
                Debug.Assert(rankCount >= 0);
            }
            else
            {
                rankCount = _topBucket.UserCount;
                TreeNode node = _root;

                while (node.UserBucket == null)
                {
                    Debug.Assert(node.Left != null && node.Right != null);
                    if (userValue.CompareTo(node.Right.LeftUser) < 0)
                    {
                        node = node.Left!;
                    }
                    else
                    {
                        rankCount += node.Left!.Count;
                        node = node.Right!;
                    }
                }

                UserBucket bucket = node.UserBucket!;
                int userIndexInBucket = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, userValue);
                Debug.Assert(userIndexInBucket >= 0);
                rankCount += userIndexInBucket;
            }

            return new RankingListResponse
            {
                User = userValue.ToUser(),
                Rank = rankCount + 1
            };
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            topN = Math.Min(topN, GetRankingCount());
            RankingListResponse[] result = new RankingListResponse[topN];
            int rankCount = 0;
            for (; rankCount < topN && rankCount < _topBucket.UserCount; rankCount++)
            {
                result[rankCount] = new RankingListResponse
                {
                    User = _topBucket.Users[rankCount].ToUser(),
                    Rank = rankCount + 1
                };
            }
            if (rankCount < topN)
            {
                int newRankCount = 0;
                int requiredCount = topN - rankCount;
                UserValue[] users = new UserValue[requiredCount];
                GetTopN(_root, requiredCount, ref newRankCount, ref users);
                for (int i = 0; i < requiredCount; i++, rankCount++)
                {
                    result[rankCount] = new RankingListResponse
                    {
                        User = users[i].ToUser(),
                        Rank = rankCount + 1
                    };
                }
            }

            return result;
        }

        private static void GetTopN(TreeNode node, int topN, ref int rankCount, ref UserValue[] result)
        {
            if (node.UserBucket != null)
            {
                int requiredCount = Math.Min(topN - rankCount, node.UserBucket.UserCount);
                Array.Copy(node.UserBucket.Users, 0, result, rankCount, requiredCount);
                rankCount += requiredCount;
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
        private static void GetAroundUserStep1(TreeNode node, UserValue user, int aroundN, ref int rankCount,
            ref int leftCount, ref int rightCount, ref UserValue[] result)
        {
            if (node.UserBucket != null)
            {
                UserBucket bucket = node.UserBucket;
                int userIndexInBucket = bucket.IndexOf(user);
                Debug.Assert(userIndexInBucket >= 0);
                rankCount += userIndexInBucket;
                // 左边
                leftCount = Math.Min(userIndexInBucket, aroundN);
                // 右边
                rightCount = Math.Min(bucket.UserCount - userIndexInBucket - 1, aroundN);
                Array.Copy(bucket.Users, userIndexInBucket - leftCount, result, aroundN - leftCount,
                    leftCount + rightCount + 1);
                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            if (user.CompareTo(node.Right.LeftUser) < 0)
            {
                GetAroundUserStep1(node.Left, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
                // 找到用户后，进入第二阶段
                if (rightCount < aroundN)
                {
                    GetAroundUserStep2(node.Right, aroundN, false, ref rightCount, ref result);
                }
            }
            else
            {
                rankCount += node.Left.Count;
                GetAroundUserStep1(node.Right, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
                // 找到用户后，进入第二阶段
                if (leftCount < aroundN)
                {
                    GetAroundUserStep2(node.Left, aroundN, true, ref leftCount, ref result);
                }
            }
        }

        private static void GetAroundUserStep2(TreeNode node, int aroundN, bool isRequiredLeft, ref int requiredCount,
            ref UserValue[] result)
        {
            if (node.UserBucket != null)
            {
                UserBucket bucket = node.UserBucket;
                if (isRequiredLeft)
                {
                    // 缺少左边的用户
                    for (int i = bucket.UserCount - 1; i >= 0 && requiredCount < aroundN; i--, requiredCount++)
                    {
                        result[aroundN - requiredCount - 1] = bucket.Users[i];
                    }
                }
                else
                {
                    // 缺少右边的用户
                    for (int i = 0; i < bucket.UserCount && requiredCount < aroundN; i++, requiredCount++)
                    {
                        result[aroundN + requiredCount + 1] = bucket.Users[i];
                    }
                }

                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            TreeNode[] children = isRequiredLeft ? [node.Right, node.Left] : [node.Left, node.Right];
            foreach (TreeNode child in children)
            {
                GetAroundUserStep2(child, aroundN, isRequiredLeft, ref requiredCount, ref result);
                if (requiredCount >= aroundN)
                {
                    break;
                }
            }
        }

        private int GetAroundUser(UserValue userValue, int aroundN, out int rankCount, out UserValue[] result)
        {
            rankCount = 0;
            result = new UserValue[aroundN * 2 + 1];
            int leftCount;
            int rightCount;
            if (userValue.CompareTo(_topBucket.MaxUser) <= 0)
            {
                rankCount = _topBucket.IndexOf(userValue);
                leftCount = Math.Min(aroundN, rankCount);
                rightCount = Math.Min(aroundN, _topBucket.UserCount - rankCount - 1);
                Array.Copy(_topBucket.Users, rankCount - leftCount, result, aroundN - leftCount,
                    leftCount + rightCount + 1);
                if (rightCount < aroundN)
                {
                    int newRankCount = 0;
                    int requiredCount = aroundN - rightCount - 1;
                    int newLeftCount = 0;
                    int newRightCount = 0;
                    UserValue[] newResult = new UserValue[requiredCount * 2 + 1];
                    GetAroundUserStep1(_root, _root.LeftUser, requiredCount, ref newRankCount, ref newLeftCount,
                        ref newRightCount, ref newResult);
                    Array.Copy(newResult, requiredCount, result, aroundN + rightCount + 1, newRightCount + 1);
                    rightCount += newRightCount + 1;
                }
            }
            else
            {
                leftCount = 0;
                rightCount = 0;
                GetAroundUserStep1(_root, userValue, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
                rankCount += _topBucket.UserCount;

                if (leftCount < aroundN)
                {
                    // 缺少左边的用户
                    int requiredCount = Math.Min(aroundN - leftCount, _topBucket.UserCount);
                    Array.Copy(_topBucket.Users, _topBucket.UserCount - requiredCount, result,
                        aroundN - leftCount - requiredCount, requiredCount);
                    leftCount += requiredCount;
                }
            }

            return leftCount + rightCount + 1;
        }

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            UserValue user = _userMap[userId];
            int count = GetAroundUser(user, aroundN, out int rankCount, out UserValue[] result);
            RankingListResponse[] responses = new RankingListResponse[count];
            for (int i = 0, ti = 0; i < 2 * aroundN + 1; i++)
            {
                if (result[i].Id == 0) continue;
                responses[ti] = new RankingListResponse
                {
                    User = result[i].ToUser(),
                    Rank = rankCount - aroundN + i + 1,
                };
                ti++;
            }

            return responses;
        }

        public int GetRankingCount()
        {
            return _root.Count + _topBucket.UserCount;
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

        /// <summary>
        /// 每个桶
        /// </summary>
        class UserBucket
        {
            public UserValue MinUser => Users[0];
            public UserValue MaxUser => Users[UserCount - 1];

            public UserValue[] Users { get; }
            public int UserCount { get; private set; }
            public bool Full => UserCount >= Users.Length;
            public bool Empty => UserCount == 0;
            public int IndexOf(UserValue user) => Array.BinarySearch(Users, 0, UserCount, user);

            public UserBucket(UserValue[] users, int userCount)
            {
                Users = users;
                UserCount = userCount;
            }

            public int Insert(UserValue user)
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

            public int Remove(UserValue user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                Debug.Assert(index >= 0);

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
            public UserBucket Split(UserValue user, out int userIndex)
            {
                int mid = UserCount / 2;
                userIndex = Array.BinarySearch(Users, 0, UserCount, user);
                if (userIndex < 0)
                {
                    userIndex = ~userIndex;
                }

                UserValue[] newUsers = new UserValue[BucketSize];
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

        enum ColorEnum : byte
        {
            Red = 0,
            Black = 1,
        }

        class TreeNode
        {
            public int Count;
            public UserValue LeftUser;
            public UserValue RightUser;
            public TreeNode? Left;
            public TreeNode? Right;
            public TreeNode? Parent;
            public UserBucket? UserBucket;
            public bool Full => Count >= BucketSize;
            public bool Empty => Count == 0;
            public ColorEnum Color = ColorEnum.Red;

            public void MoveFromChild(TreeNode child)
            {
                Debug.Assert(child.Count == Count);
                // Count = child.Count;
                // LeftUser = child.LeftUser;
                // RightUser = child.RightUser;
                Left = child.Left;
                Right = child.Right;
                child.Left?.Parent = this;
                child.Right?.Parent = this;
                UserBucket = child.UserBucket;
#if DEBUG
                child.UserBucket = null;
                child.Count = 0;
                child.Left = null;
                child.Right = null;
                child.Parent = null;
#endif
            }

            private static void UpdateLeftUser(TreeNode node)
            {
                while (node.Parent != null && node == node.Parent.Left)
                {
                    node.Parent.LeftUser = node.LeftUser;
                    node = node.Parent;
                }
            }

            private static void UpdateRightUser(TreeNode node)
            {
                while (node.Parent != null && node == node.Parent.Right)
                {
                    node.Parent.RightUser = node.RightUser;
                    node = node.Parent;
                }
            }

            public int Insert(UserValue user)
            {
                Debug.Assert(UserBucket != null);
                int userIndexInBucket = UserBucket.Insert(user);
                if (userIndexInBucket == 0)
                {
                    LeftUser = user;
                    UpdateLeftUser(this);
                }
                else if (userIndexInBucket == UserBucket.UserCount - 1)
                {
                    RightUser = user;
                    UpdateRightUser(this);
                }

                Count++;
                return userIndexInBucket;
            }

            public void Remove(UserValue user)
            {
                Debug.Assert(UserBucket != null);
                int userIndexInBucket = UserBucket.Remove(user);
                if (UserBucket.Empty)
                {
                    // LeftUser = null;
                    // RightUser = null;
                    if (Parent != null)
                    {
                        if (this == Parent.Left)
                        {
                            Parent.LeftUser = Parent.Right!.LeftUser;
                            UpdateLeftUser(Parent);
                        }
                        else
                        {
                            Parent.RightUser = Parent.Left!.RightUser;
                            UpdateRightUser(Parent);
                        }
                    }
                }
                else if (userIndexInBucket == 0)
                {
                    LeftUser = UserBucket.MinUser;
                    UpdateLeftUser(this);
                }
                else if (userIndexInBucket == UserBucket.UserCount)
                {
                    RightUser = UserBucket.MaxUser;
                    UpdateRightUser(this);
                }

                Count--;
            }

            public void Split(UserValue user, out int userIndexInBucket)
            {
                Debug.Assert(UserBucket != null);
                UserBucket newBucket = UserBucket.Split(user, out userIndexInBucket);
                Left = new TreeNode()
                {
                    UserBucket = UserBucket,
                    Count = UserBucket.UserCount,
                    LeftUser = UserBucket.MinUser,
                    RightUser = UserBucket.MaxUser,
                    Parent = this
                };
                Right = new TreeNode()
                {
                    UserBucket = newBucket,
                    Count = newBucket.UserCount,
                    LeftUser = newBucket.MinUser,
                    RightUser = newBucket.MaxUser,
                    Parent = this
                };
                UserBucket = null;
                Count++;
                if (userIndexInBucket == 0)
                {
                    UpdateLeftUser(Left);
                }
                else if (userIndexInBucket == Count - 1)
                {
                    UpdateRightUser(Right);
                }

                Debug.Assert(Count == Left.Count + Right.Count);
            }

            public void CombineChild()
            {
                Debug.Assert(Left != null && Right != null);
                Debug.Assert(Left.UserBucket != null && Right.UserBucket != null);
                UserBucket = Left.UserBucket;
                UserBucket.Combine(Right.UserBucket);
                Debug.Assert(UserBucket.UserCount == Count);
                Debug.Assert(UserBucket.MinUser.Id == LeftUser.Id);
                Debug.Assert(UserBucket.MaxUser.Id == RightUser.Id);
                Left = null;
                Right = null;
            }
        }

        readonly struct UserValue : IComparable<UserValue>
        {
            public readonly int Id;
            public readonly int Score;
            public readonly DateTime LastActive;

            public int CompareTo(UserValue other)
            {
                if (Score == other.Score)
                    return -LastActive.CompareTo(other.LastActive);
                return -Score.CompareTo(other.Score);
            }

            public User ToUser()
            {
                return new User()
                {
                    Id = Id,
                    Score = Score,
                    LastActive = LastActive
                };
            }

            public UserValue(User user)
            {
                Id = user.Id;
                Score = user.Score;
                LastActive = user.LastActive;
            }
        }
    }
}
/*
=== 排行榜测试框架 ===

=== 测试 TreeBRTreeBucketRankingListST 排行榜 ===
初始用户数: 1000000
操作数: 1000000

=== 验证操作结果与基准对比 ===
√ 所有操作结果验证通过！
测试操作结果已保存到 TreeBRTreeBucketRankingListST_test_results.json

=== 测试结果 ===
排行榜名称: TreeBRTreeBucketRankingListST
总耗时: 2198 ms
平均耗时: 2.20 ms/1000操作
内存占用: 970.10 MB
内存峰值: 970.10 MB
测试日期: 2026/2/11 15:33:28

=== 与基准 TreeBRTreeBucketRankingListS 的对比 ===
总耗时: 2198 ms vs 2167 ms (+1.43%)
平均耗时: 2.20 ms/1000操作 vs 2.17 ms/1000操作 (+1.43%)
内存占用: 970.10 MB vs 951.78 MB (+1.92%)
内存峰值: 970.10 MB vs 952.71 MB (+1.82%)

=== 单项操作耗时测试 ===

【AddUser】
  操作数: 10000 vs 10000
  总耗时: 2 ms vs 2 ms (0.00%) (10000次操作)
  平均耗时: 0.20 ms/1000操作 vs 0.20 ms/1000操作 (0.00%)
【UpdateUser】
  操作数: 20000 vs 20000
  总耗时: 19 ms vs 22 ms (-13.64%) (20000次操作)
  平均耗时: 0.95 ms/1000操作 vs 1.10 ms/1000操作 (-13.64%)
【GetUserRank】
  操作数: 30000 vs 30000
  总耗时: 20 ms vs 22 ms (-9.09%) (30000次操作)
  平均耗时: 0.67 ms/1000操作 vs 0.73 ms/1000操作 (-9.09%)
【GetTopN】
  操作数: 20000 vs 20000
  总耗时: 76 ms vs 71 ms (+7.04%) (20000次操作)
  平均耗时: 3.80 ms/1000操作 vs 3.55 ms/1000操作 (+7.04%)
【GetAroundUser】
  操作数: 20000 vs 20000
  总耗时: 29 ms vs 30 ms (-3.33%) (20000次操作)
  平均耗时: 1.45 ms/1000操作 vs 1.50 ms/1000操作 (-3.33%)
*/
// 更慢了
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RankingListNew
{
    public class BucketBRTreeRankingList : IRankingList
    {
        private Tree _tree;
        private Dictionary<int, User> _userMap;

        public BucketBRTreeRankingList(Span<User> users)
        {
            users.Sort();
            _tree = new Tree(users);

            _userMap = new(users.Length);
            foreach (ref readonly User u in users)
            {
                _userMap[u.Id] = u;
            }
        }

        public BucketBRTreeRankingList(List<User> users) :
            this(CollectionsMarshal.AsSpan(users))
        {
        }

        public int AddUser(User user)
        {
            Debug.Assert(!_userMap.ContainsKey(user.Id));
            _userMap.Add(user.Id, user);
            int rankCount = _tree.AddUser(user);

            return rankCount;
        }

        public int UpdateUser(User newUser)
        {
            User oldUser = _userMap[newUser.Id];
            _tree.RemoveUser(oldUser);
            int rankCount = _tree.AddUser(newUser);
            _userMap[newUser.Id] = newUser;
            return rankCount;
        }

        public int GetUserRank(int userId)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            User user = _userMap[userId];
            return _tree.GetUserRank(user);
        }

        public User[] GetTopN(int topN)
        {
            return _tree.GetTopN(topN);
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            User user = _userMap[userId];
            return _tree.GetAroundUser(user, aroundN);
        }

        public int GetRankingCount()
        {
            return _tree.GetRankingCount();
        }

        public void DebugPrint()
        {
#if DEBUG
            _tree.DebugPrint();
#endif
        }

        class Tree
        {
            private TreeNode _root;
#if DEBUG
            private int _addCount;
            private int _addCompareCount = 0;
            private int _removeCount = 0;
            private int _removeCompareCount = 0;
            private int _getRankCount = 0;
            private int _getRankCompareCount = 0;
#endif

            public Tree(Span<User> users)
            {
                UserBucket[] buckets = BuildBucket(users);
                int maxDepth = (int)Math.Ceiling(Math.Log(buckets.Length - 1, 2)) + 1;
                // 没有用户
                _root = users.Length == 0
                    ? new TreeNode()
                    {
                        UserBucket = new UserBucket(new User[UserBucket.BucketSize], 0),
                    }
                    : BuildTree(0, buckets.Length, 1, maxDepth, buckets);
                _root.Color = ColorEnum.Black;
#if DEBUG
                if (users.Length > 0)
                    CheckTree();
#endif
            }

            private static UserBucket[] BuildBucket(Span<User> users)
            {
                // 初始化bucket
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

            private static TreeNode BuildTree(int l, int r, int depth, int maxDepth, UserBucket[] buckets)
            {
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
                node.Left = BuildTree(l, mid, depth + 1, maxDepth, buckets);
                node.Left.Parent = node;
                node.LeftUser = node.Left.LeftUser;
                node.Right = BuildTree(mid, r, depth + 1, maxDepth, buckets);
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
                Debug.Assert(
                    node.Left == null || node.Right == null || node.Left.Count + node.Right.Count == node.Count);
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
            /// <summary>
            /// 添加玩家到排行榜
            /// </summary>
            /// <param name="user">要添加的玩家</param>
            /// <returns>玩家的排名（从0开始）</returns>
            public int AddUser(User user)
            {
#if DEBUG
                _addCount++;
#endif
                // 如果树为空，直接添加
                if (_root.Count == 0)
                {
                    UserBucket bucket = _root.UserBucket!;
                    bucket.Users[0] = user;
                    bucket.UserCount = 1;
                    _root.Count = 1;
                    _root.LeftUser = user;
                    _root.RightUser = user;
                    return 0;
                }

                int rankCount = 0;
                TreeNode node = _root;
                // 步骤1：遍历红黑树，找到目标叶子节点
                while (node.Right != null) // 判断是否为叶子节点
                {
                    node.Count++;
#if DEBUG
                    _addCompareCount++;
#endif
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
                        // 红色必定不是根节点，因此父节点必定存在
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
            /// <summary>
            /// 从排行榜中删除玩家
            /// </summary>
            /// <param name="user">要删除的玩家</param>
            public void RemoveUser(User user)
            {
#if DEBUG
                _removeCount++;
#endif
                // 步骤1：遍历红黑树，找到目标叶子节点
                TreeNode node = _root;
                while (node.Right != null)
                {
                    node.Count--; // 同步更新路径上每个节点的计数
                    node = user.CompareTo(node.Right!.LeftUser) < 0 ? node.Left! : node.Right!;
#if DEBUG
                    _removeCompareCount++;
#endif
                }

                // 步骤2：从桶中删除玩家
                node.Remove(user);
                if (node == _root) // 如果为根节点，直接返回
                    return;

                TreeNode parent = node.Parent!;
                ColorEnum parentColor = parent.Color;
                TreeNode siblingNode = parent.Left == node ? parent.Right! : parent.Left!;
                ColorEnum siblingColor = siblingNode.Color;
                bool needDelete = false;
                if (node.Empty)// 桶空了，需要合并
                {
                    // 用兄弟节点替换父节点
                    parent.MoveFromChild(siblingNode);
                    needDelete = true;
                }
                else if (siblingNode.UserBucket != null
                         && node.Count < UserBucket.CombineBucketSize
                         && siblingNode.Count < UserBucket.CombineBucketSize)
                {
                    // 桶太小，需要合并
                    parent.CombineChild();
                }

                if (needDelete)
                {
                    parent.Color = ColorEnum.Black;

                    // 如果父节点和兄弟节点都是黑色，合并后会少一个黑节点
                    if (parentColor == ColorEnum.Black && siblingColor == ColorEnum.Black)
                    {
                        // 调整红黑树平衡
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

            /// <summary>
            /// 获取玩家的当前排名
            /// </summary>
            /// <param name="user">目标玩家</param>
            /// <returns>玩家排名（从0开始）</returns>
            public int GetUserRank(User user)
            {
#if DEBUG
                _getRankCount++;
#endif
                int rankCount = 0;
                TreeNode node = _root;

                // 步骤1：遍历红黑树，累加排名
                while (node.Right != null)
                {
                    Debug.Assert(node.Left != null && node.Right != null);
                    // 根据区间判断应该进入哪个子树
                    if (user.CompareTo(node.Right.LeftUser) < 0)
                    {
                        // 用户在左子树，不累加排名
                        node = node.Left;
                    }
                    else
                    {
                        // 用户在右子树，累加左子树的用户数
                        rankCount += node.Left.Count;
                        node = node.Right;
                    }
#if DEBUG
                    _getRankCompareCount++;
#endif
                }

                // 步骤2：在桶内找到用户索引
                UserBucket bucket = node.UserBucket!;
                int userIndexInBucket = bucket.IndexOf(user);
                Debug.Assert(userIndexInBucket >= 0);
                rankCount += userIndexInBucket;
                return rankCount;
            }

            /// <summary>
            /// 获取排行榜前N名玩家
            /// </summary>
            /// <param name="topN">要获取的玩家数量</param>
            /// <returns>按排名排序的玩家数组</returns>
            public User[] GetTopN(int topN)
            {
                TreeNode node = _root;

                // 步骤1：找到最左边的叶子节点（排名最小的用户）
                while (node.Left != null)
                {
                    node = node.Left;
                }

                // 步骤2：准备结果数组
                UserBucket bucket = node.UserBucket!;
                topN = Math.Min(topN, _root.Count);
                User[] result = new User[topN];
                int rankCount = 0;

                // 步骤3：复制第一个桶的用户
                int n = Math.Min(bucket.UserCount, topN - rankCount);
                Array.Copy(bucket.Users, 0, result, rankCount, n);
                rankCount += n;

                // 步骤4：继续获取后续桶的用户
                while (rankCount < topN)
                {
                    // 步骤4a：向上查找，直到当前节点是父节点的左子节点
                    while (node != node.Parent!.Left)
                    {
                        node = node.Parent;
                    }

                    // 步骤4b：跳转到父节点的右子树
                    node = node.Parent!.Right!;

                    // 步骤4c：在右子树中找到最左边的叶子节点
                    while (node.Left != null)
                    {
                        node = node.Left;
                    }

                    // 步骤4d：复制桶内用户
                    bucket = node.UserBucket!;
                    n = Math.Min(bucket.UserCount, topN - rankCount);
                    Array.Copy(bucket.Users, 0, result, rankCount, n);
                    rankCount += n;
                }

                return result;
            }

            /// <summary>
            /// 获取目标玩家周围的排名
            /// </summary>
            /// <param name="user">目标玩家</param>
            /// <param name="aroundN">左右各获取的玩家数量</param>
            /// <returns>玩家数组和目标玩家的排名</returns>
            public (User[], int) GetAroundUser(User user, int aroundN)
            {
                int rankCount = 0;
                TreeNode node = _root;

                // 1. 找到对应的位置
                while (node.Right != null)
                {
                    Debug.Assert(node.Left != null && node.Right != null);
                    if (user.CompareTo(node.Right.LeftUser) < 0)
                    {
                        node = node.Left;
                    }
                    else
                    {
                        rankCount += node.Left.Count;
                        node = node.Right;
                    }
                }

                UserBucket bucket = node.UserBucket!;
                int userIndexInBucket = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, user);
                Debug.Assert(userIndexInBucket >= 0);
                rankCount += userIndexInBucket;

                // 2. 准备结果
                int offset = 0; // 结果数组内的偏移，用于处理用户排名过靠前，存在数据空位的情况
                int leftNum = aroundN, rightNum = aroundN; // 需求数目

                // 处理边界情况
                if (rankCount < aroundN)
                {
                    // 用户排名过靠前，无法获取足够的左边用户
                    leftNum = rankCount;
                    offset = rankCount - aroundN;
                }

                if (rankCount + aroundN + 1 > _root.Count)
                {
                    // 用户排名过靠后，无法获取足够的右边用户
                    rightNum = _root.Count - rankCount - 1;
                }

                User[] result = new User[leftNum + rightNum + 1];

                // 3. 把桶内的用户填充到结果数组中
                // 左边计数
                int leftCount = Math.Min(userIndexInBucket, leftNum);
                // 右边计数
                int rightCount = Math.Min(bucket.UserCount - userIndexInBucket - 1, rightNum);
                Array.Copy(bucket.Users, userIndexInBucket - leftCount, result, aroundN - leftCount + offset,
                    leftCount + rightCount + 1);

                // 4. 获取缺少的用户
                TreeNode tNode = node;
                while (leftCount < leftNum)
                {
                    // 查找tNode的左区间的叶子节点
                    while (tNode != tNode.Parent!.Right)
                    {
                        tNode = tNode.Parent;
                    }
                    // 跳转到父节点的左子树
                    tNode = tNode.Parent!.Left!;
                    // 找到左子树的最右节点
                    while (tNode.Right != null)
                    {
                        tNode = tNode.Right;
                    }
                    // 复制桶内用户（从末尾开始
                    bucket = tNode.UserBucket!;
                    int n = Math.Min(bucket.UserCount, leftNum - leftCount);
                    Array.Copy(bucket.Users, bucket.UserCount - n, result, aroundN - leftCount - n + offset, n);
                    leftCount += n;
                }

                // 步骤5：获取右边缺少的用户
                tNode = node;
                while (rightCount < rightNum)
                {
                    // 向上查找，直到当前节点是父节点的左子节点
                    while (tNode != tNode.Parent!.Left)
                    {
                        tNode = tNode.Parent;
                    }
                    // 跳转到父节点的右子树
                    tNode = tNode.Parent!.Right!;
                    while (tNode.Left != null)
                    {
                        tNode = tNode.Left;
                    }
                    // 复制桶内用户（从开头开始）
                    bucket = tNode.UserBucket!;
                    int n = Math.Min(bucket.UserCount, rightNum - rightCount);
                    Array.Copy(bucket.Users, 0, result, aroundN + rightCount + 1 + offset, n);
                    rightCount += n;
                }

                return (result, rankCount);
            }

            public int GetRankingCount()
            {
                return _root.Count;
            }

#if DEBUG
            public void DebugPrint()
            {
                List<(int depth, int count)> results = [];
                DebugPrint(_root, 0, ref results);
                for (int i = 0; i < results.Count; i++)
                {
                    Console.Write($"{results[i].depth}-{results[i].count}  ");
                }

                Console.WriteLine(
                    $"AddUser调用次数：{_addCount}，比较次数：{_addCompareCount}，平均比较次数：{(double)_addCompareCount / _addCount}");
                Console.WriteLine(
                    $"RemoveUser调用次数：{_removeCount}，比较次数：{_removeCompareCount}，平均比较次数：{(double)_removeCompareCount / _removeCount}");
                Console.WriteLine(
                    $"GetUserRank调用次数：{_getRankCount}，比较次数：{_getRankCompareCount}，平均比较次数：{(double)_getRankCompareCount / _getRankCount}");
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
        }

        enum ColorEnum : byte
        {
            Red = 0,
            Black = 1,
        }

        class TreeNode
        {
            public int Count;
            public User LeftUser;
            public User RightUser;
            public TreeNode? Left;
            public TreeNode? Right;
            public TreeNode? Parent;
            public UserBucket? UserBucket;
            public bool Full => Count >= UserBucket.BucketSize;
            public bool Empty => Count == 0;
            public ColorEnum Color = ColorEnum.Red;

            public void MoveFromChild(TreeNode child)
            {
                Debug.Assert(child.Count == Count);
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

            public int Insert(User user)
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

            public void Remove(User user)
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

            public void Split(User user, out int userIndexInBucket)
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
                // if (Left.UserBucket == null)
                // {
                //     Left.CombineChild();
                // }

                // if (Right.UserBucket == null)
                // {
                //     Right.CombineChild();
                // }

                Debug.Assert(Left.UserBucket != null && Right.UserBucket != null);
                UserBucket = Left.UserBucket;
                UserBucket.Combine(Right.UserBucket);
                Debug.Assert(UserBucket.UserCount == Count);
                Debug.Assert(UserBucket.MinUser.CompareTo(LeftUser) == 0);
                Debug.Assert(UserBucket.MaxUser.CompareTo(RightUser) == 0);
                Left = null;
                Right = null;
            }
        }
    }
}

/*
测试类: BucketBRTreeRankingList
== Test stau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: AddUser
排行榜用户数: 200000
总耗时: 23 ms
平均耗时: 0.23 ms/1000操作
内存占用: 9.30 MB
内存峰值: 13.05 MB
测试日期: 2026/3/8 22:25:53
== Test stau10w_10w End ===

== Test stgau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetAroundUser
排行榜用户数: 100000
总耗时: 52 ms
平均耗时: 0.52 ms/1000操作
内存占用: 36.66 MB
内存峰值: 36.68 MB
测试日期: 2026/3/8 22:25:54
== Test stgau10w_10w End ===

== Test stgt10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetTopN
排行榜用户数: 100000
总耗时: 26 ms
平均耗时: 0.26 ms/1000操作
内存占用: 80.88 MB
内存峰值: 80.96 MB
测试日期: 2026/3/8 22:25:55
== Test stgt10w_10w End ===

== Test stgu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetUserRank
排行榜用户数: 100000
总耗时: 27 ms
平均耗时: 0.27 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:25:56
== Test stgu10w_10w End ===

== Test stuu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: UpdateUser
排行榜用户数: 100000
总耗时: 59 ms
平均耗时: 0.59 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/8 22:25:56
== Test stuu10w_10w End ===

== Test t100w_100w ===
用户数: 1000000
操作数: 1000000
排行榜用户数: 1099921
总耗时: 544 ms
平均耗时: 0.54 ms/1000操作
内存占用: 251.84 MB
内存峰值: 251.83 MB
测试日期: 2026/3/8 22:25:58
== Test t100w_100w End ===

== Test t10w_10w ===
用户数: 100000
操作数: 100000
排行榜用户数: 109905
总耗时: 23 ms
平均耗时: 0.23 ms/1000操作
内存占用: 29.07 MB
内存峰值: 32.83 MB
测试日期: 2026/3/8 22:26:01
== Test t10w_10w End ===
*/
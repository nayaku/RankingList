using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RankingListNew
{
    public class BRTreeRankingList : IRankingList
    {
        private TreeNode _root;
        private Dictionary<int, User> _userMap;

        public BRTreeRankingList(Span<User> users)
        {
            users.Sort();
            int maxDepth = (int)Math.Ceiling(Math.Log(users.Length - 1, 2)) + 1;
            _root = users.Length == 0 ? new TreeNode() : BuildTree(0, users.Length, 1, maxDepth, users);
            _root.Color = ColorEnum.Black;
            _userMap = new(users.Length);
            foreach (ref readonly User u in users)
            {
                _userMap[u.Id] = u;
            }
#if DEBUG
            if (users.Length > 0)
                CheckTree();
#endif
        }

        public BRTreeRankingList(List<User> users) :
            this(CollectionsMarshal.AsSpan(users))
        {
        }

        private static TreeNode BuildTree(int l, int r, int depth, int maxDepth, Span<User> users)
        {
            // 初始化tree
            TreeNode node = new()
            {
                Color = (maxDepth - depth) % 2 == 0 ? ColorEnum.Red : ColorEnum.Black
            };
            if (l + 1 == r)
            {
                node.Count = 1;
                node.LeftUser = users[l];
                node.RightUser = users[l];
                return node;
            }

            int mid = (l + r) >> 1;
            node.Left = BuildTree(l, mid, depth + 1, maxDepth, users);
            node.Left.Parent = node;
            node.LeftUser = node.Left.LeftUser;
            node.Right = BuildTree(mid, r, depth + 1, maxDepth, users);
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
        private int AddTreeUser(User user)
        {
            int rankCount = 0;
            if (_root.Count == 0)
            {
                _root.Count = 1;
                _root.LeftUser = user;
                _root.RightUser = user;
                return rankCount;
            }
            TreeNode node = _root;
            while (node.Right != null)
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
            rankCount += node.Add(user);
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
        private void RemoveTreeUser(User user)
        {
            TreeNode node = _root;
            while (node.Right != null)
            {
                node.Count--;
                node = user.CompareTo(node.Right!.LeftUser) < 0 ? node.Left! : node.Right!;
            }

            // 叶子节点
            Debug.Assert(node.LeftUser.CompareTo(user) == 0);
            if (node == _root)
            {
                node.Count--;
                return;
            }

            TreeNode parent = node.Parent!;
            ColorEnum parentColor = parent.Color;
            TreeNode siblingNode = parent.Left == node ? parent.Right! : parent.Left!;
            ColorEnum siblingColor = siblingNode.Color;
            node.Remove();
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

        public int AddUser(User user)
        {
            Debug.Assert(!_userMap.ContainsKey(user.Id));
            _userMap.Add(user.Id, user);
            int rankCount = AddTreeUser(user);

            return rankCount;
        }

        public int UpdateUser(User newUser)
        {
            User oldUser = _userMap[newUser.Id];
            RemoveTreeUser(oldUser);
            int rankCount = AddTreeUser(newUser);
            _userMap[newUser.Id] = newUser;
            return rankCount;
        }

        public int GetUserRank(int userId)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            User user = _userMap[userId];
            int rankCount = 0;
            TreeNode node = _root;

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
            Debug.Assert(node.LeftUser.CompareTo(user) == 0);

            return rankCount;
        }

        public User[] GetTopN(int topN)
        {
            TreeNode node = _root;

            // 获取排名靠前的叶子节点
            while (node.Left != null)
            {
                node = node.Left;
            }
            topN = Math.Min(topN, GetRankingCount());
            User[] result = new User[topN];
            int rankCount = 0;
            result[rankCount++] = node.LeftUser;

            // 缺少的用户数
            while (rankCount < topN)
            {
                // 查找tNode的右区间的叶子节点
                while (node != node.Parent!.Left)
                {
                    node = node.Parent;
                }

                node = node.Parent!.Right!;
                while (node.Left != null)
                {
                    node = node.Left;
                }
                result[rankCount++] = node.LeftUser;
            }
            return result;
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            User user = _userMap[userId];
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

            Debug.Assert(node.LeftUser.CompareTo(user) == 0);

            // 2. 准备结果
            int offset = 0; // 结果数组内的偏移，用于处理用户排名过靠前，存在数据空位的情况
            int leftNum = aroundN, rightNum = aroundN; // 需求数目
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
            int leftCount = 0;
            // 右边计数
            int rightCount = 0;
            result[aroundN + offset] = node.LeftUser;

            // 4. 获取缺少的用户
            TreeNode tNode = node;
            while (leftCount < leftNum)
            {
                // 查找tNode的左区间的叶子节点
                while (tNode != tNode.Parent!.Right)
                {
                    tNode = tNode.Parent;
                }

                tNode = tNode.Parent!.Left!;
                while (tNode.Right != null)
                {
                    tNode = tNode.Right;
                }

                result[aroundN - leftCount - 1 + offset] = tNode.LeftUser;
                leftCount++;
            }

            tNode = node;
            while (rightCount < rightNum)
            {
                // 查找tNode的右区间的叶子节点
                while (tNode != tNode.Parent!.Left)
                {
                    tNode = tNode.Parent;
                }

                tNode = tNode.Parent!.Right!;
                while (tNode.Left != null)
                {
                    tNode = tNode.Left;
                }

                result[aroundN + rightCount + 1 + offset] = tNode.LeftUser;
                rightCount++;
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
            List<int> results = [];
            DebugPrint(_root, 0, ref results);
            for (int i = 0; i < results.Count; i++)
            {
                Console.Write($"{results[i]}  ");
                // 每20个换行
                if ((i + 1) % 20 == 0)
                {
                    Console.WriteLine();
                }
            }
        }

        private void DebugPrint(TreeNode node, int depth, ref List<int> results)
        {
            if (node.Left == null)
            {
                results.Add(depth);
                return;
            }

            DebugPrint(node.Left, depth + 1, ref results);
            DebugPrint(node.Right, depth + 1, ref results);
        }
#endif
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
            public ColorEnum Color = ColorEnum.Red;

            public void MoveFromChild(TreeNode child)
            {
                Debug.Assert(child.Count == Count);
                Left = child.Left;
                Right = child.Right;
                child.Left?.Parent = this;
                child.Right?.Parent = this;
#if DEBUG
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

            public void Remove()
            {
                if (Parent == null)
                    return;
                if (Parent.Left == this)
                {
                    TreeNode ancleNode = Parent.Right!;
                    Parent.LeftUser = ancleNode.LeftUser;
                    Parent.MoveFromChild(ancleNode!);
                    UpdateLeftUser(Parent);
                }
                else
                {
                    TreeNode ancleNode = Parent.Left!;
                    Parent.RightUser = ancleNode.RightUser;
                    Parent.MoveFromChild(Parent.Left!);
                    UpdateRightUser(Parent);
                }
            }

            public int Add(User user)
            {
                bool isLeft = user.CompareTo(LeftUser) < 0;
                if (isLeft)
                    LeftUser = user;
                else
                    RightUser = user;
                Left = new()
                {
                    Count = 1,
                    Parent = this,
                    LeftUser = LeftUser,
                    RightUser = LeftUser
                };
                Right = new()
                {
                    Count = 1,
                    Parent = this,
                    LeftUser = RightUser,
                    RightUser = RightUser,
                };
                if (isLeft)
                    UpdateLeftUser(this);
                else
                    UpdateRightUser(this);
                Count++;

                Debug.Assert(Count == Left.Count + Right.Count);
                return isLeft ? 0 : 1;
            }
        }
    }

}

/*
== Test stau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: AddUser
排行榜用户数: 200000
总耗时: 24 ms
平均耗时: 0.24 ms/1000操作
内存占用: 21.55 MB
内存峰值: 25.34 MB
测试日期: 2026/3/7 18:48:03
√ 所有操作结果验证通过！
总耗时: 24 ms vs 31 ms (-22.58%)
平均耗时: 0.24 ms/1k操作 vs 0.31 ms/1k操作 (-22.58%)
内存占用: 21.55 MB vs 9.30 MB (+131.76%)
内存峰值: 25.34 MB vs 13.05 MB (+94.15%)
== Test stau10w_10w End ===

== Test stgau10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetAroundUser
排行榜用户数: 100000
总耗时: 119 ms
平均耗时: 1.19 ms/1000操作
内存占用: 36.66 MB
内存峰值: 36.68 MB
测试日期: 2026/3/7 18:48:04
√ 所有操作结果验证通过！
总耗时: 119 ms vs 85 ms (+40.00%)
平均耗时: 1.19 ms/1k操作 vs 0.85 ms/1k操作 (+40.00%)
内存占用: 36.66 MB vs 36.66 MB (0.00%)
内存峰值: 36.68 MB vs 36.68 MB (0.00%)
== Test stgau10w_10w End ===

== Test stgt10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetTopN
排行榜用户数: 100000
总耗时: 66 ms
平均耗时: 0.66 ms/1000操作
内存占用: 80.88 MB
内存峰值: 80.97 MB
测试日期: 2026/3/7 18:48:06
√ 所有操作结果验证通过！
总耗时: 66 ms vs 30 ms (+120.00%)
平均耗时: 0.66 ms/1k操作 vs 0.30 ms/1k操作 (+120.00%)
内存占用: 80.88 MB vs 80.88 MB (0.00%)
内存峰值: 80.97 MB vs 80.96 MB (0.00%)
== Test stgt10w_10w End ===

== Test stgu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: GetUserRank
排行榜用户数: 100000
总耗时: 32 ms
平均耗时: 0.32 ms/1000操作
内存占用: 2.29 MB
内存峰值: 2.30 MB
测试日期: 2026/3/7 18:48:08
√ 所有操作结果验证通过！
总耗时: 32 ms vs 28 ms (+14.29%)
平均耗时: 0.32 ms/1k操作 vs 0.28 ms/1k操作 (+14.29%)
内存占用: 2.29 MB vs 2.29 MB (+0.02%)
内存峰值: 2.30 MB vs 2.30 MB (0.00%)
== Test stgu10w_10w End ===

== Test stuu10w_10w ===
用户数: 100000
操作数: 100000
限制操作类型: UpdateUser
排行榜用户数: 100000
总耗时: 77 ms
平均耗时: 0.77 ms/1000操作
内存占用: 2.29 MB
内存峰值: 17.61 MB
测试日期: 2026/3/7 18:48:09
√ 所有操作结果验证通过！
总耗时: 77 ms vs 43 ms (+79.07%)
平均耗时: 0.77 ms/1k操作 vs 0.43 ms/1k操作 (+79.07%)
内存占用: 2.29 MB vs 2.29 MB (0.00%)
内存峰值: 17.61 MB vs 2.30 MB (+663.97%)
== Test stuu10w_10w End ===

== Test t100w_100w ===
用户数: 1000000
操作数: 1000000
排行榜用户数: 1099921
总耗时: 1392 ms
平均耗时: 1.39 ms/1000操作
内存占用: 263.91 MB
内存峰值: 275.40 MB
测试日期: 2026/3/7 18:48:11
√ 所有操作结果验证通过！
总耗时: 1392 ms vs 560 ms (+148.57%)
平均耗时: 1.39 ms/1k操作 vs 0.56 ms/1k操作 (+148.57%)
内存占用: 263.91 MB vs 251.84 MB (+4.79%)
内存峰值: 275.40 MB vs 251.83 MB (+9.36%)
== Test t100w_100w End ===
*/
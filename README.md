[TOC]

# 游戏全服排行榜

# 一、概述
在游戏和社交应用中，排行榜是一个核心功能模块，需要支持高并发、实时的增改查和区间查询操作。本文设计了一个由 `区间红黑树 + 分桶` 混合数据结构实现的高性能排行榜。区间红黑树管理区间和桶，分桶存储玩家的有序数据。查询操作均能稳定在O(log M + log K)（M为桶数量，K为单桶玩家数），增改操作性能在O(log M + log K + K)以内。

# 二、设计思路
本文采用 `区间红黑树 + 分桶` 的混合数据结构，结合两者的优势，形成互补：

- 分桶：将所有玩家按分数范围划分为多个桶，每个桶内部存储少量有序玩家。插入、删除操作和查询在单桶内进行。

- 区间红黑树：用红黑树管理所有桶。红黑树的非叶子节点包含区间，叶子节点包含分桶指针。

## QA：

**为什么不直接用纯红黑树？**

**内存局部性差**：红黑树节点分散在堆上，查询区间用户（排行榜前N名玩家、查询用户排名周围玩家）时， CPU 缓存命中率低，随机访问导致性能下降。增改的时候，插入/删除操作导致树的旋转，需要随机访问和读写大量节点，导致缓存不命中。（事实上也证明了如此，见测试数据：）

# 三、数据结构设计

## 3.1 排行榜接口设计
首先需要定义一个排行榜，一个游戏的排行榜应该包含以下操作：
- 添加玩家
- 更新玩家分数
- 获取某个玩家的排名
- 获取排行榜前N名玩家
- 获取玩家周围的排名
- 获得玩家总数

```csharp
public interface IRankingList
{
    int AddUser(User user);
    int UpdateUser(User user);
    int GetUserRank(int userId);
    User[] GetTopN(int topN);
    (User[], int) GetAroundUser(int userId, int aroundN);
    int GetRankingCount();
}
```

## 3.2 用户数据结构：
用户数据结构包含玩家的唯一标识符（Id）、分数（Score）和最后更新时间（LastUpdateTime）。用户数据结构实现了 `IComparable<User>` 接口，用于在排行榜中进行排序。
排序规则：
- 首先根据分数降序排序
- 如果分数相同，则根据最后更新时间升序排序
- 如果最后更新时间也相同，则根据玩家ID升序排序

```csharp
public readonly struct User : IComparable<User>
{
    public readonly int Id;
    public readonly int Score;
    public readonly DateTime LastUpdateTime;

    public int CompareTo(User other)
    {
        if (Score != other.Score)
            return -Score.CompareTo(other.Score);
        else if (LastUpdateTime != other.LastUpdateTime)
            return LastUpdateTime.CompareTo(other.LastUpdateTime);
        else
            return Id.CompareTo(other.Id);
    }
}
```

这里采用了结构体来表示玩家，因为玩家的信息不会改变，所以采用结构体可以避免频繁的内存分配。

## 3.3 用户桶
用户桶是指将所有玩家按照分数分为多个桶，每个桶内采用有序数组存储玩家。

```csharp
class UserBucket
{
    public User MinUser => Users[0];
    public User MaxUser => Users[UserCount - 1];
    public User[] Users;
    public int UserCount;
    public bool Full => UserCount >= Users.Length;
    public bool Empty => UserCount == 0;
    public int IndexOf(User user) => Array.BinarySearch(Users, 0, UserCount, user);
}
```

**关键字段** ：

- Users：有序数组，用于存储玩家数据。
- UserCount：当前桶内玩家数量。
- IndexOf：使用二分查找找到玩家在桶内的索引位置。


核心操作：
- 添加玩家：根据玩家分数找到对应的桶，使用二分查找找到插入位置，将玩家插入到桶内有序数组中。
- 删除玩家：使用二分查找找到玩家位置并删除。
- 分裂桶：将桶内玩家平均分为两部分，桶的后半段进入新桶，前半段保持在原桶。分裂的时候，肯定为插入新节点时发现桶满的时候，插入和分裂同时进行，可以提升性能。
- 合并桶：向后合并桶，将后一个桶内的玩家合并到当前桶的末尾。

完整的用户桶实现如下：
```csharp
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
```

## 3.4 树节点

红黑树节点，非叶子节点存储子树统计信息，叶子节点关联一个用户桶。

```csharp
class TreeNode
{
    public int Count;
    public User LeftUser;
    public User RightUser;
    public TreeNode? Left;
    public TreeNode? Right;
    public TreeNode? Parent;
    public UserBucket? UserBucket;
    public bool Full => Count >= BucketSize;
    public bool Empty => Count == 0;
    public ColorEnum Color = ColorEnum.Red;
}
```

**关键字段** ：

- Count ：子树中的用户总数
- LeftUser/RightUser ：子树的最小/最大用户（用于区间判断）
- Left/Right/Parent ：树结构指针
- UserBucket ：叶子节点的用户桶
- Color ：红黑树颜色标记

**核心操作**：
- 移动赋值：将子节点的信息赋值给当前节点。
- 添加玩家：插入到新用户到叶子节点的用户桶中。如果插入的用户影响到子树的最小/最大用户，从下往上更新子树的最小/最大用户。
- 删除玩家：从叶子节点的用户桶中删除玩家。同上检查并更新所有子树的最小/最大用户。
- 分裂节点：新建2个子节点。将原先的桶分配左儿子，将新桶分配给右儿子。同上检查并更新子树的最小/最大用户。
- 合并节点：左右儿子必须为叶子节点才能进行此操作。将右儿子的桶合并到左儿子的桶中。


移动赋值：
```csharp
public void MoveFromChild(TreeNode child)
{
    Left = child.Left;
    Right = child.Right;
    child.Left?.Parent = this;
    child.Right?.Parent = this;
    UserBucket = child.UserBucket;
}
```

当区间变化的时候，维护子树的最小/最大用户：
```csharp
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
```

添加玩家：
```csharp
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
```

删除玩家：这里需要处理一个特殊情况，当桶被清空的时候，需要更新父节点的最小/最大用户为兄弟节点的最小/最大用户，并向上一路更新子树。
```csharp
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
```

分裂节点：
```csharp
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
```

合并节点：
```csharp
public void CombineChild()
{
    Debug.Assert(Left != null && Right != null);
    Debug.Assert(Left.UserBucket != null && Right.UserBucket != null);
    UserBucket = Left.UserBucket;
    UserBucket.Combine(Right.UserBucket);
    Left = null;
    Right = null;
}
```

## 3.5 排行榜设计

```csharp
public class BucketBRTreeRankingList : IRankingList
{
    private static readonly int BucketSize = 256; // 每个bucket的用户数量
    private static readonly int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量
    private TreeNode _root;
    private Dictionary<int, User> _userMap;
}
```
`_root` 是根节点，`_userMap` 是用户id到用户的映射。

添加玩家：
更新玩家分数：
获取某个玩家的排名：
获取排行榜前N名玩家：
获取玩家周围的排名：

# 完整代码

最后完整代码
```csharp
public class BucketBRTreeRankingList : IRankingList
{
    private static readonly int BucketSize = 256; // 每个bucket的用户数量
    private static readonly int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量
    private TreeNode _root;
    private Dictionary<int, User> _userMap;

    public BucketBRTreeRankingList(Span<User> users)
    {
        users.Sort();
        UserBucket[] buckets = BuildBucket(users);
        // 没有用户
        int maxDepth = (int)Math.Ceiling(Math.Log(buckets.Length - 1, 2)) + 1;
        _root = users.Length == 0 ? new TreeNode() : BuildTree(0, buckets.Length, 1, maxDepth, buckets);
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

    public BucketBRTreeRankingList(List<User> users) :
        this(CollectionsMarshal.AsSpan(users))
    {
    }

    private static UserBucket[] BuildBucket(Span<User> users)
    {
        // 初始化bucket
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
    private void AddUser(User user, ref int rankCount)
    {
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
    private void RemoveUser(User user)
    {
        TreeNode node = _root;
        while (node.Right != null)
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
        else if (siblingNode.UserBucket != null && parent.Count < (BucketSize >> 2))
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

    public int AddUser(User user)
    {
        Debug.Assert(!_userMap.ContainsKey(user.Id));
        _userMap.Add(user.Id, user);
        int rankCount = 0;
        if (_root.Count == 0)
        {
            User[] bucketUsers = new User[BucketSize];
            bucketUsers[0] = user;
            _root.UserBucket = new UserBucket(bucketUsers, 1);
            _root.Count = 1;
            _root.LeftUser = user;
            _root.RightUser = user;
        }
        else
        {
            AddUser(user, ref rankCount);
        }

        return rankCount;
    }

    public int UpdateUser(User newUser)
    {
        User oldUser = _userMap[newUser.Id];
        RemoveUser(oldUser);
        int rankCount = 0;
        AddUser(newUser, ref rankCount);
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

        UserBucket bucket = node.UserBucket!;
        int userIndexInBucket = bucket.IndexOf(user);
        Debug.Assert(userIndexInBucket >= 0);
        rankCount += userIndexInBucket;
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
        UserBucket bucket = node.UserBucket!;
        topN = Math.Min(topN, GetRankingCount());
        User[] result = new User[topN];
        int rankCount = 0;
        int n = Math.Min(bucket.UserCount, topN - rankCount);
        Array.Copy(bucket.Users, 0, result, rankCount, n);
        rankCount += n;

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

            bucket = node.UserBucket!;
            n = Math.Min(bucket.UserCount, topN - rankCount);
            Array.Copy(bucket.Users, 0, result, rankCount, n);
            rankCount += n;
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

        UserBucket bucket = node.UserBucket!;
        int userIndexInBucket = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, user);
        Debug.Assert(userIndexInBucket >= 0);
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

            tNode = tNode.Parent!.Left!;
            while (tNode.Right != null)
            {
                tNode = tNode.Right;
            }

            bucket = tNode.UserBucket!;
            int n = Math.Min(bucket.UserCount, leftNum - leftCount);
            Array.Copy(bucket.Users, bucket.UserCount - n, result, aroundN - leftCount - n + offset, n);
            leftCount += n;
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
        public bool Full => Count >= BucketSize;
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

    /// <summary>
    /// 每个桶
    /// </summary>
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


```
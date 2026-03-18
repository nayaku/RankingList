[TOC]

# 游戏全服排行榜

# 一、概述

## 1.1 背景介绍

在游戏和社交应用中，排行榜是一个核心功能模块。无论是展示全服战力榜、竞技场积分榜，还是好友排行榜，都需要支持以下核心操作：

- **添加玩家**：新玩家进入排行榜
- **更新分数**：玩家分数变化后重新排名
- **查询排名**：获取某个玩家的当前排名
- **获取前N名**：展示排行榜前列玩家
- **获取周围玩家**：展示目标玩家附近的排名情况

这些操作需要**高并发、实时响应**，能够承载**数百万玩家**在线、**每秒百万级请求**的访问压力。

## 1.2 问题分析

### 传统方案的局限性

在设计排行榜系统之前，我们先分析几种常见方案：

**方案一：有序数组**

直接按分数排序，支持二分查找排名。
<font color="#27ae60">
优点：

- √ 取排名 O(log n)，二分查找
- √ 取范围用户高效
- √ 内存连续，缓存友好
</font>
<font color="#c0392b">
缺点：

- × 插入/删除操作 O(n)，需要移动大量元素
- × 不适合大量用户的场景（如百万级用户）
</font>

**方案二：分桶**

将玩家按分数范围分桶，每个桶内维护一个有序数组。桶存放在数组中。

<font color="#27ae60">
优点：

- √ 插入/更新 O(M + log K + K)，M为桶数，K为桶内元素
- √ 查找排名 O(M + log K)，M为玩家数，K为桶数
- √ 保持了桶内内存连续，缓存友好
</font>

<font color="#c0392b">
缺点：

- × 桶数M增加，分裂桶的时候，需要移动大量桶引用。
- × 桶数M增加，查找排名操作时间复杂度增加。
</font>

**方案三：分桶 + 链表**

<font color="#27ae60">
优点：

- √ 保持了分桶的优势
- √ 分裂桶的时候，不需要移动大量的桶引用。
</font>

<font color="#c0392b">
缺点：

- × 桶数M增加，查找排名操作时间复杂度增加。
</font>

**方案四：分桶 + 跳表**

<font color="#27ae60">
优点：

- √ 插入/更新 O(log M + log K + K)，M为桶数，K为桶内元素
- √ 查找排名 O(log M + log K)，M为玩家数，K为桶数
</font>

<font color="#c0392b">
缺点：

- × 内存局部性差。跳表节点分散在堆上，不连续，不容易缓存命中。
</font>


**方案五：纯红黑树**

<font color="#27ae60">
优点：

- √ 插入/更新 O(log N)，N为玩家数
- √ 查找排名 O(log N)，N为玩家数
</font>

<font color="#c0392b">
缺点：

- × 内存局部性差。节点分散在堆上，不连续，不容易缓存命中。
- × 范围操作效率低。需要大量遍历红黑树节点。
</font>

**方案六：分桶 + 红黑树**

<font color="#27ae60">
优点：

- √ 保持了分桶的优势
- √ 插入/更新 O(log M + log K + K)，M为桶数，K为桶内元素
- √ 查找排名 O(log M + log K)，M为玩家数，K为桶数
</font>
<font color="#c0392b">
</font>

方案六也是本文采用方案。比较有争议的是方案五（分桶 + 跳表）和方案六（分桶 + 红黑树）。尽管分桶 + 红黑树在插入/更新操作后有概率需要调整树平衡，且节点对比次数大于分桶 + 跳表，但是由于树节点较小且较为集中，反而容易命中CPU缓冲。最终方案六在实际场景中综合耗时更短。具体的分析可以参考第x章。

## 1.3 数据结构设计

本文设计了一个由 **分桶 + 红黑树** 混合数据结构实现的高性能排行榜：

```
┌─────────────────────────────────────────────────────────────┐
│                        红黑树（管理桶）                        │
│                           根节点                             │
│                          /      \                           │
│                     左子树      右子树                        │
│                       /            \                        │
│                  ...              ...                       │
│                   /                  \                      │
│              叶子节点              叶子节点                    │
│                 ↓                    ↓                      │
│            ┌─────────┐         ┌─────────┐                  │
│            │  桶 1   │         │  桶 2    │  ...             │
│            │[用户数组]│         │ [用户数组]│                  │
│            └─────────┘         └─────────┘                  │
└─────────────────────────────────────────────────────────────┘
```

**核心思想**：
- **分桶**：将玩家按分数范围划分为多个桶，每个桶内部存储少量有序玩家
- **区间红黑树**：用红黑树管理所有桶，非叶子节点存储区间信息，叶子节点关联桶

**性能优势**：
- 查询操作稳定在 **O(log M + log K)**（M 为桶数量，K 为单桶玩家数）
- 增改操作性能在 **O(log M + log K + K)** 以内
- 内存局部性好，CPU 缓存命中率高

# 二、设计思路

## 2.1 核心设计理念

### 分桶（Bucket）
将所有玩家按分数范围划分为多个桶，每个桶内部存储少量有序玩家。插入、删除操作和查询在单桶内进行。

**优势**：
- 桶内使用有序数组，内存连续，缓存友好
- 批量操作可以使用 `Array.Copy`，底层会利用 SIMD 指令并行复制，性能提升数倍
- 桶内操作时间复杂度 O(log K + K)，K 为桶大小

### 区间红黑树（Interval Red-Black Tree）
用红黑树管理所有桶。红黑树的非叶子节点包含区间信息，叶子节点包含分桶指针。

**优势**：
- 桶定位时间复杂度 O(log M)，M 为桶数量
- 自动平衡，保证树高度稳定
- 支持快速排名计算（利用节点计数）

# 三、数据结构设计

## 3.1 排行榜接口设计

```csharp
public interface IRankingList
{
    /// <summary>
    /// 添加玩家到排行榜
    /// </summary>
    /// <param name="user">要添加的玩家</param>
    /// <returns>玩家的排名（从0开始）</returns>
    int AddUser(User user);

    /// <summary>
    /// 更新玩家分数（先删除旧数据，再插入新数据）
    /// </summary>
    /// <param name="user">包含新分数的玩家信息</param>
    /// <returns>玩家的新排名</returns>
    int UpdateUser(User user);

    /// <summary>
    /// 获取玩家的当前排名
    /// </summary>
    /// <param name="userId">玩家ID</param>
    /// <returns>玩家排名（从0开始）</returns>
    int GetUserRank(int userId);

    /// <summary>
    /// 获取排行榜前N名玩家
    /// </summary>
    /// <param name="topN">要获取的玩家数量</param>
    /// <returns>按排名排序的玩家数组</returns>
    User[] GetTopN(int topN);

    /// <summary>
    /// 获取目标玩家周围的排名
    /// </summary>
    /// <param name="userId">目标玩家ID</param>
    /// <param name="aroundN">左右各获取的玩家数量</param>
    /// <returns>玩家数组和目标玩家的排名</returns>
    (User[], int) GetAroundUser(int userId, int aroundN);

    /// <summary>
    /// 获取排行榜中的玩家总数
    /// </summary>
    /// <returns>玩家数量</returns>
    int GetRankingCount();
}
```

## 3.2 用户数据结构

用户数据结构包含玩家的唯一标识符（Id）、分数（Score）和最后更新时间（LastUpdateTime）。用户数据结构实现了 `IComparable<User>` 接口，用于在排行榜中进行排序。

### 排序规则

1. 首先根据分数降序排序
2. 如果分数相同，则根据最后更新时间升序排序
3. 如果最后更新时间也相同，则根据玩家ID升序排序

### 代码实现

```csharp
/// <summary>
/// 用户数据结构，表示排行榜中的一个玩家
/// 采用结构体（struct）而非类（class），避免频繁的堆内存分配
/// </summary>
public readonly struct User : IComparable<User>
{
    /// <summary>
    /// 玩家的唯一标识符
    /// </summary>
    public readonly int Id;

    /// <summary>
    /// 玩家的分数
    /// </summary>
    public readonly int Score;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public readonly DateTime LastUpdateTime;

    public User(int id, int score, DateTime lastUpdateTime)
    {
        Id = id;
        Score = score;
        LastUpdateTime = lastUpdateTime;
    }

    /// <summary>
    /// 比较方法，实现 IComparable 接口
    /// 排序规则：分数降序 → 更新时间升序 → ID升序
    /// </summary>
    public int CompareTo(User other)
    {
        int compareResult = -Score.CompareTo(other.Score);
        if (compareResult != 0) 
            return compareResult;
        compareResult = LastUpdateTime.CompareTo(other.LastUpdateTime);
        if (compareResult != 0) 
            return compareResult;
        return Id.CompareTo(other.Id);
    }
}
```

用户数据结构采用结构体（struct）而非类（class），避免频繁的堆内存分配。结构体数组在内存中连续存储，提高缓存命中率；玩家信息不会改变，使用值类型更安全。

## 3.3 用户桶

用户桶是排行榜的核心数据结构之一，负责存储和管理一组连续排名的玩家。每个桶内部采用有序数组存储玩家，利用数组的连续内存特性提高缓存命中率。

### 3.3.1 数据结构定义

```csharp
/// <summary>
/// 用户桶，存储一组连续排名的玩家
/// 桶内玩家按分数有序排列，使用有序数组实现
/// </summary>
class UserBucket
{
    public const int BucketSize = 256; // 每个bucket的用户数量

    public const int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量
    
    public const int CombineBucketSize = BucketSize / 8; // 当小于这个数值的时候，合并桶
    /// <summary>
    /// 桶内分数最小的玩家（排名最高的玩家）
    /// </summary>
    public User MinUser => Users[0];

    /// <summary>
    /// 桶内分数最大的玩家（排名最低的玩家）
    /// </summary>
    public User MaxUser => Users[UserCount - 1];

    /// <summary>
    /// 存储玩家的有序数组
    /// 数组大小固定为 BucketSize（默认256）
    /// </summary>
    public User[] Users;

    /// <summary>
    /// 当前桶内的玩家数量
    /// </summary>
    public int UserCount;

    /// <summary>
    /// 桶是否已满
    /// </summary>
    public bool Full => UserCount >= Users.Length;

    /// <summary>
    /// 桶是否为空
    /// </summary>
    public bool Empty => UserCount == 0;

    /// <summary>
    /// 使用二分查找定位玩家在桶内的位置
    /// </summary>
    /// <param name="user">要查找的玩家</param>
    /// <returns>玩家索引，如果不存在返回负数</returns>
    public int IndexOf(User user) => Array.BinarySearch(Users, 0, UserCount, user);
}
```

### 3.3.2 核心操作详解

#### 插入玩家 (Insert)

插入操作需要保持数组的有序性：

```csharp
/// <summary>
/// 向桶内插入一个玩家，保持有序性
/// </summary>
/// <param name="user">要插入的玩家</param>
/// <returns>玩家在桶内的索引位置</returns>
public int Insert(User user)
{
    // 步骤1：使用二分查找找到插入位置
    // Array.BinarySearch 返回负数表示未找到，取反后得到插入位置
    int index = Array.BinarySearch(Users, 0, UserCount, user);
    if (index < 0)
    {
        index = ~index;  // 取反得到正确的插入位置
    }

    // 步骤2：移动元素，为新玩家腾出空间
    // 将 [index, UserCount-1] 的元素向后移动一位
    Array.Copy(Users, index, Users, index + 1, UserCount - index);

    // 步骤3：插入新玩家
    Users[index] = user;
    UserCount++;

    return index;
}
```

#### 删除玩家 (Remove)

删除操作同样需要保持数组的连续性：

```csharp
/// <summary>
/// 从桶内删除一个玩家
/// </summary>
/// <param name="user">要删除的玩家</param>
/// <returns>被删除玩家的原索引位置</returns>
public int Remove(User user)
{
    // 步骤1：使用二分查找定位玩家
    int index = Array.BinarySearch(Users, 0, UserCount, user);

    // 步骤2：移动元素，填补空缺
    if (index < UserCount)
    {
        Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
    }

    UserCount--;
    return index;
}
```

#### 分裂桶 (Split)

当桶满时，需要分裂为两个桶。分裂操作与插入操作合并进行，提升性能：

```csharp
/// <summary>
/// 将桶分裂为两个桶，同时插入新玩家
/// 分裂策略：将后半部分玩家移到新桶
/// </summary>
/// <param name="user">要插入的新玩家</param>
/// <param name="userIndex">输出参数，玩家在分裂后的索引</param>
/// <returns>新创建的桶（包含后半部分玩家）</returns>
public UserBucket Split(User user, out int userIndex)
{
    // 步骤1：计算分裂点（中间位置）
    int mid = UserCount / 2;

    // 步骤2：确定新玩家的插入位置
    userIndex = Array.BinarySearch(Users, 0, UserCount, user);
    if (userIndex < 0)
    {
        userIndex = ~userIndex;
    }

    // 步骤3：创建新桶
    User[] newUsers = new User[BucketSize];
    int newUserCount = UserCount - mid;

    // 步骤4：根据新玩家位置决定如何分裂
    if (userIndex >= mid)
    {
        // 新玩家在新桶中
        // 复制 [mid, userIndex-1] 到新桶
        Array.Copy(Users, mid, newUsers, 0, userIndex - mid);
        // 插入新玩家
        newUsers[userIndex - mid] = user;
        // 复制 [userIndex, UserCount-1] 到新桶
        Array.Copy(Users, userIndex, newUsers, userIndex - mid + 1, UserCount - userIndex);
        newUserCount++;
    }
    else
    {
        // 新玩家在原桶中
        // 复制 [mid, UserCount-1] 到新桶
        Array.Copy(Users, mid, newUsers, 0, UserCount - mid);
    }

    // 步骤5：更新原桶
    UserCount = mid;
    UserBucket newBucket = new(newUsers, newUserCount);

    // 如果新玩家在原桶中，执行插入
    if (userIndex < mid)
        Insert(user);

    return newBucket;
}
```
#### 合并桶 (Combine)

当桶内玩家过少时，需要与相邻桶合并：

```csharp
/// <summary>
/// 将另一个桶的玩家合并到当前桶
/// </summary>
/// <param name="other">要合并的桶</param>
public void Combine(UserBucket other)
{
    // 将 other 的玩家复制到当前桶的末尾
    Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
    UserCount += other.UserCount;
}
```

#### 完整代码：

```csharp
/// <summary>
/// 用户桶
/// 桶内玩家按分数有序排列，使用有序数组实现
/// </summary>
internal class UserBucket
{
    public const int BucketSize = 256; // 每个bucket的用户数量
    public const int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量

    /// <summary>
    /// 桶内分数最小的玩家（排名最高的玩家）
    /// </summary>
    public User MinUser => Users[0];

    /// <summary>
    /// 桶内分数最大的玩家（排名最低的玩家）
    /// </summary>
    public User MaxUser => Users[UserCount - 1];

    /// <summary>
    /// 存储玩家的有序数组
    /// 数组大小固定为 BucketSize
    /// </summary>
    public User[] Users;

    /// <summary>
    /// 当前桶内的玩家数量
    /// </summary>
    public int UserCount;

    /// <summary>
    /// 桶是否已满
    /// </summary>
    public bool Full => UserCount >= Users.Length;

    /// <summary>
    /// 桶是否为空
    /// </summary>
    public bool Empty => UserCount == 0;

    /// <summary>
    /// 使用二分查找定位玩家在桶内的位置
    /// </summary>
    /// <param name="user">要查找的玩家</param>
    /// <returns>玩家索引，如果不存在返回负数</returns>
    public int IndexOf(User user) => Array.BinarySearch(Users, 0, UserCount, user);

    public UserBucket(User[] users, int userCount)
    {
        Users = users;
        UserCount = userCount;
    }

    /// <summary>
    /// 向桶内插入一个玩家，保持数组有序性
    /// </summary>
    /// <param name="user">要插入的玩家</param>
    /// <returns>玩家在桶内的索引位置</returns>

    public int Insert(User user)
    {
        // 步骤1：使用二分查找找到插入位置
        // Array.BinarySearch 返回负数表示未找到，取反后得到插入位置
        int index = Array.BinarySearch(Users, 0, UserCount, user);
        if (index < 0)
        {
            index = ~index;  // 取反得到正确的插入位置
        }

        // 步骤2：移动元素，为新玩家腾出空间
        // 将 [index, UserCount-1] 的元素向后移动一位
        if (index < Users.Length)
        {
            Array.Copy(Users, index, Users, index + 1, UserCount - index);
        }

        // 步骤3：插入新玩家
        Users[index] = user;
        UserCount++;

        return index;
    }

    /// <summary>
    /// 从桶内删除指定玩家
    /// </summary>
    /// <param name="user">要删除的玩家</param>
    /// <returns>被删除玩家的原索引位置</returns>
    public int Remove(User user)
    {
        // 步骤1：使用二分查找定位玩家
        int index = Array.BinarySearch(Users, 0, UserCount, user);
        Debug.Assert(index >= 0);

        // 步骤2：移动元素，填补空缺
        if (index < UserCount)
        {
            Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
        }

        UserCount--;
        return index;
    }

    /// <summary>
    /// 将桶分裂为两个桶，同时插入新玩家
    /// 分裂策略：将后半部分玩家移到新桶
    /// </summary>
    /// <param name="user">要插入的新玩家</param>
    /// <param name="userIndex">输出参数，玩家在分裂后的索引</param>
    /// <returns>新创建的桶（包含后半部分玩家）</returns>
    public UserBucket Split(User user, out int userIndex)
    {
        // 步骤1：计算分裂点（中间位置）
        int mid = UserCount / 2;

        // 步骤2：确定新玩家的插入位置
        userIndex = Array.BinarySearch(Users, 0, UserCount, user);
        if (userIndex < 0)
        {
            userIndex = ~userIndex;
        }

        // 步骤3：创建新桶
        User[] newUsers = new User[BucketSize];
        int newUserCount = UserCount - mid;

        // 步骤4：根据新玩家位置决定如何分裂
        if (userIndex >= mid)
        {
            // 新玩家在新桶中
            Array.Copy(Users, mid, newUsers, 0, userIndex - mid);
            newUsers[userIndex - mid] = user;
            Array.Copy(Users, userIndex, newUsers, userIndex - mid + 1, UserCount - userIndex);
            newUserCount++;
        }
        else
        {
            // 新玩家在原桶中
            Array.Copy(Users, mid, newUsers, 0, UserCount - mid);
        }

        // 步骤5：更新原桶
        UserCount = mid;
        UserBucket newBucket = new(newUsers, newUserCount);

        // 如果新玩家在原桶中，执行插入
        if (userIndex < mid)
            Insert(user);
        return newBucket;
    }
    
    /// <summary>
    /// 将另一个桶的玩家合并到当前桶
    /// </summary>
    /// <param name="other">要合并的桶</param>
    public void Combine(UserBucket other)
    {
        // 将 other 的玩家复制到当前桶的末尾
        Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
        UserCount += other.UserCount;
    }
}
```

### 为什么选择Array而不是List？

桶内存储用的是 `User[]` 数组，而不是 `List<User>`。原因有几个：

**1. 内存布局**

List 内部虽然也是数组，但多了一层封装。对于固定大小的桶来说，数组更直接。少一次间接访问，性能更好。

**2. 桶大小固定**

桶的大小是固定的，不需要动态扩容。List 的动态扩容能力在这里用不上，反而增加了每次判断`EnsureCapacity`的开销。

**3. 减少对象分配**

数组直接分配，List 还要分配一个包装对象。在高频操作场景下，少一个对象就少一次 GC 压力。

## 3.4 树节点

树节点是红黑树的核心组成部分，分为两种类型：
- **非叶子节点**：存储子树统计信息（区间、计数），用于快速定位和排名计算
- **叶子节点**：关联一个用户桶，存储实际的玩家数据

### 3.4.1 数据结构定义

```csharp
/// <summary>
/// 红黑树颜色枚举
/// </summary>
enum ColorEnum : byte
{
    Red = 0,      // 红色节点
    Black = 1,    // 黑色节点
}

/// <summary>
/// 红黑树节点
/// 非叶子节点存储子树统计信息，叶子节点关联用户桶
/// </summary>
class TreeNode
{
    /// <summary>
    /// 子树中的用户总数
    /// </summary>
    public int Count;

    /// <summary>
    /// 子树的最小用户（分数最高的用户）
    /// </summary>
    public User LeftUser;

    /// <summary>
    /// 子树的最大用户（分数最低的用户）
    /// </summary>
    public User RightUser;

    /// <summary>
    /// 左子节点
    /// </summary>
    public TreeNode? Left;

    /// <summary>
    /// 右子节点
    /// </summary>
    public TreeNode? Right;

    /// <summary>
    /// 父节点
    /// 用于向上遍历和红黑树调整
    /// </summary>
    public TreeNode? Parent;

    /// <summary>
    /// 用户桶（仅叶子节点有值）
    /// 非叶子节点此字段为 null
    /// </summary>
    public UserBucket? UserBucket;

    /// <summary>
    /// 桶是否已满（仅叶子节点有效）
    /// </summary>
    public bool Full => Count >= BucketSize;

    /// <summary>
    /// 桶是否为空（仅叶子节点有效）
    /// </summary>
    public bool Empty => Count == 0;

    /// <summary>
    /// 红黑树颜色标记
    /// 默认为红色（新插入的节点总是红色）
    /// </summary>
    public ColorEnum Color = ColorEnum.Red;
}
```

### 3.4.2 区间信息的作用

区间信息（LeftUser/RightUser）用于快速定位目标桶：

```
假设树结构如下：
                根节点
            LeftUser=A, RightUser=H
            Count=8
              /              \
         左子树              右子树
    LeftUser=A,RightUser=D  LeftUser=E,RightUser=H
    Count=4                 Count=4
       /    \                  /    \
    桶1     桶2              桶3     桶4
   [A,B]   [C,D]            [E,F]   [G,H]

查找用户 C：
1. 根节点：C < E（右子树最小值），进入左子树
2. 左子树：C >= C（右子树最小值），进入右子树
3. 到达桶2，在桶内查找 C
```

### 3.4.3 核心操作详解

#### 区间更新操作

当桶的边界用户发生变化时，需要向上更新所有祖先节点的区间信息：

```csharp
/// <summary>
/// 向上更新左边界（LeftUser）
/// 当左子树的最小用户发生变化时调用
/// </summary>
private static void UpdateLeftUser(TreeNode node)
{
    // 沿着左边界向上遍历，更新所有祖先的 LeftUser
    while (node.Parent != null && node == node.Parent.Left)
    {
        node.Parent.LeftUser = node.LeftUser;
        node = node.Parent;
    }
}

/// <summary>
/// 向上更新右边界（RightUser）
/// 当右子树的最大用户发生变化时调用
/// </summary>
private static void UpdateRightUser(TreeNode node)
{
    // 沿着右边界向上遍历，更新所有祖先的 RightUser
    while (node.Parent != null && node == node.Parent.Right)
    {
        node.Parent.RightUser = node.RightUser;
        node = node.Parent;
    }
}
```

**更新示例**：
```
插入用户 X（分数=95）到桶1：
原桶1：[A(100), B(90)]
新桶1：[A(100), X(95), B(90)]

X 成为新的最小值（插入位置=1，不是最小值，无需更新）
如果插入的是 Y（分数=110）：
新桶1：[Y(110), A(100), B(90)]
Y 成为新的最小值，需要更新：
  桶1.LeftUser = Y
  左子树.LeftUser = Y
  根节点.LeftUser = Y
```

#### 插入玩家操作

```csharp
/// <summary>
/// 向叶子节点的桶内插入玩家
/// </summary>
/// <param name="user">要插入的玩家</param>
/// <returns>玩家在桶内的索引</returns>
public int Insert(User user)
{
    Debug.Assert(UserBucket != null);  // 确保是叶子节点

    // 步骤1：在桶内插入玩家
    int userIndexInBucket = UserBucket.Insert(user);

    // 步骤2：检查是否需要更新区间信息
    if (userIndexInBucket == 0)
    {
        // 新玩家是桶内最小值，更新 LeftUser
        LeftUser = user;
        UpdateLeftUser(this);  // 向上更新所有祖先
    }
    else if (userIndexInBucket == UserBucket.UserCount - 1)
    {
        // 新玩家是桶内最大值，更新 RightUser
        RightUser = user;
        UpdateRightUser(this);  // 向上更新所有祖先
    }

    // 步骤3：更新计数
    Count++;
    return userIndexInBucket;
}
```

#### 删除玩家操作

```csharp
/// <summary>
/// 从叶子节点的桶内删除玩家
/// </summary>
/// <param name="user">要删除的玩家</param>
public void Remove(User user)
{
    Debug.Assert(UserBucket != null);

    // 步骤1：从桶内删除玩家
    int userIndexInBucket = UserBucket.Remove(user);

    // 步骤2：处理桶空的情况
    if (UserBucket.Empty)
    {
        if (Parent != null)
        {
            // 桶空了，需要用兄弟节点的边界更新父节点
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
    // 步骤3：检查是否需要更新区间信息
    else if (userIndexInBucket == 0)
    {
        // 删除的是最小值，更新 LeftUser
        LeftUser = UserBucket.MinUser;
        UpdateLeftUser(this);
    }
    else if (userIndexInBucket == UserBucket.UserCount)
    {
        // 删除的是最大值，更新 RightUser
        RightUser = UserBucket.MaxUser;
        UpdateRightUser(this);
    }

    Count--;
}
```

#### 分裂节点操作

当桶满时，需要分裂节点：

```csharp
/// <summary>
/// 分裂叶子节点，创建两个子节点
/// </summary>
/// <param name="user">要插入的新玩家</param>
/// <param name="userIndexInBucket">输出参数，玩家在分裂后的索引</param>
public void Split(User user, out int userIndexInBucket)
{
    Debug.Assert(UserBucket != null);

    // 步骤1：分裂桶，同时插入新玩家
    UserBucket newBucket = UserBucket.Split(user, out userIndexInBucket);

    // 步骤2：创建左子节点（原桶）
    Left = new TreeNode()
    {
        UserBucket = UserBucket,
        Count = UserBucket.UserCount,
        LeftUser = UserBucket.MinUser,
        RightUser = UserBucket.MaxUser,
        Parent = this
    };

    // 步骤3：创建右子节点（新桶）
    Right = new TreeNode()
    {
        UserBucket = newBucket,
        Count = newBucket.UserCount,
        LeftUser = newBucket.MinUser,
        RightUser = newBucket.MaxUser,
        Parent = this
    };

    // 步骤4：当前节点变为非叶子节点
    UserBucket = null;
    Count++;  // Count 现在表示子树节点数（2个子节点）

    // 步骤5：更新区间信息
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

#### 合并节点操作

当桶过小时，需要合并子节点：

```csharp
/// <summary>
/// 合并左右子节点的桶
/// 前提：左右子节点都是叶子节点
/// </summary>
public void CombineChild()
{
    // 步骤1：将右子节点的桶合并到左子节点的桶
    UserBucket = Left.UserBucket;
    UserBucket.Combine(Right.UserBucket);

    // 步骤2：清除子节点引用
    Left = null;
    Right = null;
}
```

#### 移动赋值操作

用于删除操作时，用子节点替换当前节点：

```csharp
/// <summary>
/// 将子节点的信息复制到当前节点
/// 用于删除操作时的节点替换
/// </summary>
/// <param name="child">要移动的子节点</param>
public void MoveFromChild(TreeNode child)
{
    Debug.Assert(child.Count == Count);

    // 复制子节点的所有信息
    Left = child.Left;
    Right = child.Right;
    child.Left?.Parent = this;
    child.Right?.Parent = this;
    UserBucket = child.UserBucket;
}
```

#### 完整代码

```csharp
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
```

## 3.5 红黑树设计

<!-- 排行榜的核心是一个红黑树，每个叶子节点关联一个用户桶。通过红黑树的平衡特性，保证所有操作的时间复杂度为 O(log M)。

### 3.5.1 整体架构

```csharp
/// <summary>
/// 排行榜核心类
/// 使用红黑树 + 桶的混合数据结构实现
/// </summary>
public class BucketBRTreeRankingList : IRankingList
{
    /// <summary>
    /// 红黑树，管理所有桶
    /// </summary>
    private Tree _tree;

    /// <summary>
    /// 用户ID到用户数据的映射
    /// 用于快速查找用户数据（O(1) 时间复杂度）
    /// </summary>
    private Dictionary<int, User> _userMap;
}
``` -->
### 3.5.1 数据结构定义

```csharp
class Tree
{
    private TreeNode _root;
}
```

### 3.5.2 红黑树规则

红黑树是一种自平衡二叉搜索树，通过颜色标记和旋转操作保持平衡。其规则如下：

1. **每个节点要么是红色，要么是黑色**（非红即黑）
2. **根节点是黑色的**
3. **所有叶子节点（NIL节点）都是黑色的**
4. **如果一个节点是红色的，那么它的两个子节点都是黑色的**（即不存在连续的红色节点）
5. **从任意节点到其每个叶子节点的所有简单路径都包含相同数量的黑色节点**（即所有路径的黑色节点数相同）

这些规则保证了红黑树的高度始终为 O(log n)，从而保证了查找、插入、删除操作的时间复杂度为 O(log n)。

> **参考资料**：
> - [一文带你彻底读懂红黑树（附详细图解） - 知乎](https://zhuanlan.zhihu.com/p/91960960)
> - [红黑树（图解+秒懂+史上最全） - 技术自由圈 - 博客园](https://www.cnblogs.com/crazymakercircle/p/16320430.html)
> - [红黑树详解-CSDN博客](https://blog.csdn.net/u014454538/article/details/120120216)

### 3.5.3 核心操作详解

#### 1. 初始化

**算法流程**：
1. 用户分桶
2. 构建红黑树

构建一个桶数组
```csharp
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
```

构建红黑树。最底层的节点染色为红色。每层颜色交替。
```csharp
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
```
构造函数
```csharp
public BucketBRTreeRankingList(Span<User> users)
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
}
```
没有用户的时候，生成一个空节点。

#### 1. 添加玩家 (AddUser)

添加玩家是最复杂的操作，涉及树的遍历、桶的插入、桶的分裂和红黑树的调整。

**算法流程**：
```
1. 如果树为空，直接添加到根节点
2. 遍历红黑树，找到目标叶子节点（桶）
   - 同时更新路径上每个节点的计数
   - 累加左子树的用户数，计算排名
3. 如果桶已满，分裂桶
   - 创建两个子节点
   - 调整红黑树平衡
4. 如果桶未满，直接插入
5. 返回玩家排名
```

**代码实现**：

```csharp
/// <summary>
/// 添加玩家到排行榜
/// </summary>
/// <param name="user">要添加的玩家</param>
/// <returns>玩家的排名（从0开始）</returns>
public int AddUser(User user)
{
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
```

#### 2. 删除玩家 (RemoveUser)

删除玩家需要处理桶空或桶过小的情况，可能涉及桶的合并。

**算法流程**：
```
1. 遍历红黑树，找到目标叶子节点（桶）
   - 同时更新路径上每个节点的计数
2. 从桶中删除玩家
3. 如果桶空了，用兄弟节点替换父节点
4. 如果桶太小，合并左右子节点的桶
5. 调整红黑树平衡
```

**代码实现**：

```csharp
public void RemoveUser(User user)
{
    // 步骤1：遍历红黑树，找到目标叶子节点
    TreeNode node = _root;
    while (node.Right != null)
    {
        node.Count--; // 同步更新路径上每个节点的计数
        node = user.CompareTo(node.Right!.LeftUser) < 0 ? node.Left! : node.Right!;
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
    if(node.Empty)// 桶空了，需要合并
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
    
    if(needDelete)
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
```

**时间复杂度分析**：
- 树遍历：O(log M)
- 桶内删除：O(log K + K)
- 红黑树调整：O(log M)
- **总时间复杂度**：O(log M + K)

#### 3. 获取玩家排名 (GetUserRank)

获取玩家排名是排行榜的核心操作之一，利用红黑树的计数信息快速计算。

**排名计算原理**：
- 红黑树按分数有序，左子树分数 > 右子树分数
- 当进入右子树时，说明左子树所有用户都在目标用户之前
- 累加所有左子树的 Count，再加上桶内索引，得到最终排名

**示例**：
```
假设树结构如下：
        根节点(Count=1000)
       /              \
   左子树(Count=400)  右子树(Count=600)

查找用户 X：
1. 如果 X 在左子树，排名 < 400
2. 如果 X 在右子树，排名 >= 400
   继续在右子树中递归计算
```

**代码实现**：

```csharp
/// <summary>
/// 获取玩家的当前排名
/// </summary>
/// <param name="user">目标玩家</param>
/// <returns>玩家排名（从0开始）</returns>
public int GetUserRank(User user)
{
    int rankCount = 0;
    TreeNode node = _root;

    // 步骤1：遍历红黑树，累加排名
    while (node.Right != null)  // 判断是否为叶子节点
    {
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
    }

    // 步骤2：在桶内找到用户索引
    UserBucket bucket = node.UserBucket!;
    int userIndexInBucket = bucket.IndexOf(user);
    rankCount += userIndexInBucket;

    return rankCount;
}
```

**时间复杂度分析**：
- 树遍历：O(log M)
- 桶内二分查找：O(log K)
- **总时间复杂度**：O(log M + log K)

#### 4. 获取前N名玩家 (GetTopN)

获取前N名玩家需要按顺序遍历桶，利用红黑树的结构高效实现。

**算法流程**：
```
1. 找到最左边的叶子节点（排名最小的用户）
2. 复制桶内用户到结果数组
3. 如果还需要更多用户，继续获取后续桶
   - 向上查找，直到当前节点是父节点的左子节点
   - 跳转到父节点的右子树
   - 找到右子树的最左节点
```

**代码实现**：

```csharp
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
    topN = Math.Min(topN, GetRankingCount());
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
```

**时间复杂度分析**：
- 找到第一个桶：O(log M)
- 遍历桶：O(N + 桶数量)
- **总时间复杂度**：O(N + log M)

#### 5. 获取玩家周围的排名 (GetAroundUser)

获取玩家周围的排名是游戏中最常用的功能之一，用于展示"我的排名"和"附近玩家"。

**算法流程**：
```
1. 找到用户所在的桶和排名
2. 计算需要获取的左右用户数量
3. 从当前桶内获取用户
4. 如果左边不够，向左遍历桶获取
5. 如果右边不够，向右遍历桶获取
```

**代码实现**：

```csharp
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

    // 步骤1：找到用户所在的桶和排名
    while (node.Right != null)
    {
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
    rankCount += userIndexInBucket;

    // 步骤2：计算需要获取的左右用户数量
    int offset = 0;
    int leftNum = aroundN, rightNum = aroundN;

    // 处理边界情况
    if (rankCount < aroundN)
    {
        leftNum = rankCount;  // 左边用户不足
        offset = rankCount - aroundN;
    }
    if (rankCount + aroundN + 1 > _root.Count)
    {
        rightNum = _root.Count - rankCount - 1;  // 右边用户不足
    }

    User[] result = new User[leftNum + rightNum + 1];

    // 步骤3：从当前桶内获取用户
    int leftCount = Math.Min(userIndexInBucket, leftNum);
    int rightCount = Math.Min(bucket.UserCount - userIndexInBucket - 1, rightNum);
    Array.Copy(bucket.Users, userIndexInBucket - leftCount, result,
               aroundN - leftCount + offset, leftCount + rightCount + 1);

    // 步骤4：获取左边缺少的用户
    TreeNode tNode = node;
    while (leftCount < leftNum)
    {
        // 向上查找，直到当前节点是父节点的右子节点
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
        // 复制桶内用户（从末尾开始）
        bucket = tNode.UserBucket!;
        int n = Math.Min(bucket.UserCount, leftNum - leftCount);
        Array.Copy(bucket.Users, bucket.UserCount - n, result,
                   aroundN - leftCount - n + offset, n);
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
        // 找到右子树的最左节点
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
```

**时间复杂度分析**：
- 找到用户桶：O(log M)
- 桶内二分查找：O(log K)
- 遍历桶：O(aroundN)
- **总时间复杂度**：O(log M + log K + aroundN)

### 3.5.4 红黑树调整操作

红黑树的调整操作是保证树平衡的关键，包括旋转和变色。

#### 左旋操作

```csharp
/// <summary>
/// 左旋操作
/// 将节点 x 的右子节点 y 提升为新的根
/// </summary>
/// <param name="x">旋转中心节点</param>
/// <returns>旋转后的新根节点</returns>
private TreeNode RotateLeft(TreeNode x)
{
    Debug.Assert(x.Right != null && x.Left != null &&
                 x.Right.Left != null && x.Right.Right != null);

    TreeNode y = x.Right;

    // 步骤1：调整子树关系
    x.Right = y.Left;
    x.Right.Parent = x;
    y.Left = x;
    y.Parent = x.Parent;
    x.Parent = y;

    // 步骤2：调整父节点关系
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
    }

    // 步骤3：更新区间信息和计数
    x.RightUser = x.Right.RightUser;
    y.LeftUser = x.LeftUser;
    x.Count = x.Left.Count + x.Right.Count;
    y.Count = y.Left.Count + y.Right.Count;

    // 步骤4：更新根节点
    if (y.Parent == null)
        _root = y;

    return y;
}
```

#### 右旋操作

```csharp
/// <summary>
/// 右旋操作
/// 将节点 x 的左子节点 y 提升为新的根
/// </summary>
/// <param name="x">旋转中心节点</param>
/// <returns>旋转后的新根节点</returns>
private TreeNode RotateRight(TreeNode x)
{
    Debug.Assert(x.Left != null && x.Left.Left != null &&
                 x.Left.Right != null && x.Right != null);

    TreeNode y = x.Left;

    // 步骤1：调整子树关系
    x.Left = y.Right;
    x.Left.Parent = x;
    y.Right = x;
    y.Parent = x.Parent;
    x.Parent = y;

    // 步骤2：调整父节点关系
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

    // 步骤3：更新区间信息和计数
    x.LeftUser = x.Left.LeftUser;
    y.RightUser = x.RightUser;
    x.Count = x.Left.Count + x.Right.Count;
    y.Count = y.Left.Count + y.Right.Count;

    // 步骤4：更新根节点
    if (y.Parent == null)
        _root = y;

    return y;
}
```

# 四、性能测试

## 4.1 测试环境

- **CPU**: AMD Ryzen 9 9700X
- **内存**: 64GB DDR5
- **操作系统**: Windows 11
- **运行时**: .NET 10.0
- **测试数据量**: 10万用户、10万次操作（混合测试为100万用户、100万次操作）

## 4.2 测试结果

测试条件：10万用户、10万次操作（混合测试为100万用户、100万次操作）

| 操作类型 | 总耗时 | 平均耗时（1000操作） |
|---------|--------|--------------------------|
| AddUser | 31 ms | 0.31 ms |
| UpdateUser | 43 ms | 0.43 ms |
| GetUserRank | 28 ms | 0.28 ms |
| GetTopN | 30 ms | 0.30 ms |
| GetAroundUser | 85 ms | 0.85 ms |
| 10倍混合* | 560 ms | 0.56 ms |

> *10倍混合：100万用户、100万次操作，概率分布 [AddUser:10%, UpdateUser:20%, GetUserRank:30%, GetTopN:20%, GetAroundUser:20%]

### 与其他实现对比


# 五、总结

## 设计要点

核心思路是 **红黑树 + 桶** 的混合结构：

1. **红黑树管理桶**：O(log M) 定位桶，O(log M + log K) 计算排名
2. **桶内有序数组**：连续内存，缓存友好，批量复制快
3. **区间信息缓存**：节点存储子树区间，加速查找
4. **计数信息维护**：节点维护子树用户数，快速计算排名

## 时间复杂度

| 操作 | 时间复杂度 |
|-----|-----------|
| 添加用户 | O(log M + K) |
| 删除用户 | O(log M + K) |
| 更新用户 | O(log M + K) |
| 获取排名 | O(log M + log K) |
| 获取前N名 | O(N + log M) |
| 获取周围玩家 | O(log M + log K + aroundN) |

M 为桶数量，K 为桶大小（256），N 为获取数量。

## 适用场景

- 大规模用户（百万级甚至千万级）
- 频繁更新（分数经常变化）
- 需要快速排名查询
- 需要批量获取（前N名、周围玩家）
- 内存敏感

## 不适用场景

- 小规模用户（<1000）：简单数组排序即可
- 极少更新：可以用更简单的结构
- 分布式场景：需要考虑分片和一致性

## 扩展方向

1. **持久化**：支持重启恢复
2. **分布式**：分片到多个节点
3. **历史排行榜**：查询历史排名
4. **多维度排序**：按多个字段排序
5. **实时推送**：排名变化时推送通知

## 参考资料

- [一文带你彻底读懂红黑树 - 知乎](https://zhuanlan.zhihu.com/p/91960960)
- [红黑树详解 - 博客园](https://www.cnblogs.com/crazymakercircle/p/16320430.html)
- [B+树详解 - 维基百科](https://zh.wikipedia.org/wiki/B%2B%E6%A0%91)

# 完整代码

```csharp
public class BucketBRTreeRankingList : IRankingList
{
    private static readonly int BucketSize = 256;
    private static readonly int InitialBucketSize = BucketSize / 2;

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
        _tree.RemoveTreeUser(oldUser);
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
        _tree.DebugPrint();
    }

    class Tree
    {
        private TreeNode _root;

        public Tree(Span<User> users)
        {
            UserBucket[] buckets = BuildBucket(users);
            int maxDepth = (int)Math.Ceiling(Math.Log(buckets.Length - 1, 2)) + 1;
            // 没有用户
            _root = users.Length == 0
                ? new TreeNode()
                {
                    UserBucket = new UserBucket(new User[BucketSize], 0),
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
        public int AddUser(User user)
        {
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
        public void RemoveTreeUser(User user)
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

        public int GetUserRank(User user)
        {
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
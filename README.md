[TOC]

# 游戏全服排行榜

# 一、概述

## 1.1 背景介绍

在游戏和社交应用中，排行榜是一个核心功能模块，广泛应用于全服战力榜、竞技场积分榜、好友排行榜等场景。一个高性能的排行榜系统需要支持以下核心操作：

- **添加玩家**：新玩家进入排行榜
- **更新分数**：玩家分数变化后重新排名
- **查询排名**：获取某个玩家的当前排名
- **获取前N名**：展示排行榜前列玩家
- **获取周围玩家**：展示目标玩家附近的排名情况

这些操作需要在**高并发**环境下提供**实时响应**，能够承载**数百万玩家**在线、**每秒百万级请求**的访问压力。

## 1.2 问题分析

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
- × CPU缓存不友好，内存局部性差。
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

方案六是本文最终采用的方案。在方案选型过程中，最具争议的是方案四（分桶 + 跳表）和方案六（分桶 + 红黑树）。虽然分桶 + 红黑树在插入/更新操作后有概率需要调整树平衡，且节点对比次数略高于分桶 + 跳表，但由于红黑树节点较小且内存分布更为集中，反而更容易命中CPU缓存，减少了内存访问延迟。从实际性能测试结果来看，分桶 + 红黑树方案在各种场景下的综合耗时更短，表现更为稳定。具体性能对比分析详见第4章。

## 1.3 数据结构设计

本文设计了一个由 **分桶 + 红黑树** 混合数据结构实现的高性能排行榜：

**核心思想**：
- **分桶**：将玩家按分数范围划分为多个桶，每个桶内部存储少量有序玩家
- **区间红黑树**：用红黑树管理所有桶，非叶子节点存储区间信息，叶子节点关联桶

**性能优势**：
- 查询操作稳定在 **O(log M + log K)**（M 为桶数量，K 为单桶玩家数）
- 增改操作性能在 **O(log M + log K + K)** 以内
- 内存局部性好，CPU 缓存命中率高

# 二、设计思路

## 2.1 核心设计理念

**分桶（Bucket）**
将所有玩家按分数范围划分为多个桶，每个桶内部存储少量有序玩家。插入、删除操作和查询在单桶内进行。

优势：
- 桶内使用有序数组，内存连续，缓存友好
- 批量操作可以使用 `Array.Copy`，底层会利用 SIMD 指令并行复制，性能提升数倍
- 桶内操作时间复杂度 O(log K + K)，K 为桶大小

**区间红黑树（Interval Red-Black Tree）**
用红黑树管理所有桶。红黑树的非叶子节点包含区间信息，叶子节点包含分桶指针。

优势：
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

**排序规则**

1. 首先根据分数降序排序
2. 如果分数相同，则根据最后更新时间升序排序
3. 如果最后更新时间也相同，则根据玩家ID升序排序

**代码实现**

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

用户桶是排行榜的核心数据结构之一，负责存储和管理一组连续排名的玩家。每个桶内部采用有序数组存储玩家，充分利用数组的连续内存特性提高CPU缓存命中率，从而提升操作效率。

### 3.3.1 数据结构定义

```csharp
/// <summary>
/// 用户桶，存储一组连续排名的玩家
/// 桶内玩家按分数有序排列，使用有序数组实现
/// </summary>
class UserBucket
{
    public const int BucketSize = 256; // 每个桶的最大容量
    public const int InitialBucketSize = BucketSize / 2; // 桶的初始容量
    public const int CombineBucketSize = BucketSize / 8; // 桶合并阈值（当桶内玩家数小于此值时触发合并）
    
    /// <summary>
    /// 桶内分数最大的玩家（排名最高的玩家）
    /// </summary>
    public User MinUser => Users[0];

    /// <summary>
    /// 桶内分数最小的玩家（排名最低的玩家）
    /// </summary>
    public User MaxUser => Users[UserCount - 1];

    /// <summary>
    /// 存储玩家的有序数组
    /// 数组大小固定为 BucketSize，确保内存连续
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

插入操作需要在保持数组有序性的前提下添加新玩家：

```csharp
/// <summary>
/// 向桶内插入一个玩家，保持数组有序性
/// </summary>
/// <param name="user">要插入的玩家</param>
/// <returns>玩家在桶内的索引位置</returns>
public int Insert(User user)
{
    // 步骤1：使用二分查找确定插入位置
    // Array.BinarySearch 返回负数表示未找到，取反后得到正确的插入位置
    int index = Array.BinarySearch(Users, 0, UserCount, user);
    if (index < 0)
    {
        index = ~index;  // 取反得到正确的插入位置
    }

    // 步骤2：移动元素，为新玩家腾出空间
    // 将 [index, UserCount-1] 范围内的元素向后移动一位
    Array.Copy(Users, index, Users, index + 1, UserCount - index);

    // 步骤3：在计算好的位置插入新玩家
    Users[index] = user;
    UserCount++;

    return index;
}
```

#### 删除玩家 (Remove)

删除操作需要从数组中移除指定玩家，并保持剩余玩家的有序性和数组的连续性：

```csharp
/// <summary>
/// 从桶内删除指定玩家
/// </summary>
/// <param name="user">要删除的玩家</param>
/// <returns>被删除玩家的原索引位置</returns>
public int Remove(User user)
{
    // 步骤1：使用二分查找定位玩家在桶内的位置
    int index = Array.BinarySearch(Users, 0, UserCount, user);

    // 步骤2：如果找到玩家，移动后续元素填补空缺
    if (index < UserCount)
    {
        // 将 [index+1, UserCount-1] 范围内的元素向前移动一位
        Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
    }

    // 步骤3：更新桶内玩家数量
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
    /// 桶内分数最大的玩家（排名最高的玩家）
    /// </summary>
    public User MinUser => Users[0];

    /// <summary>
    /// 桶内分数最小的玩家（排名最低的玩家）
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

### 3.3.3 为什么选择Array而不是List？

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
/// 红黑树节点颜色枚举
/// 使用byte类型节省内存空间
/// </summary>
enum ColorEnum : byte
{
    Red = 0,      // 红色节点
    Black = 1,    // 黑色节点
}

/// <summary>
/// 红黑树节点
/// 非叶子节点存储子树统计信息（区间、计数），叶子节点关联用户桶
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
    /// 用于向上遍历和红黑树平衡调整
    /// </summary>
    public TreeNode? Parent;

    /// <summary>
    /// 用户桶引用
    /// 仅叶子节点有值，非叶子节点为null
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
    /// 默认为红色（根据红黑树规则，新插入的节点总是红色）
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

排行榜的核心是一个红黑树，每个叶子节点关联一个用户桶。通过红黑树的平衡特性，保证所有操作的时间复杂度为 O(log M)。

### 3.5.1 数据结构定义

```csharp
class Tree
{
    private TreeNode _root;
}
```
_root 是树的根节点。

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
/// <summary>
/// 从排行榜中删除玩家
/// </summary>
/// <param name="user">要删除的玩家</param>
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

#### 3. 获取玩家排名 (GetUserRank)

获取玩家排名是排行榜的核心操作之一，利用红黑树的维护的区间计数，就可以快速计算玩家的排名。

**排名计算原理**：
- 红黑树按分数有序，左子树 < 右子树
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

获取前N名玩家需要按顺序遍历桶。

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

排行榜实现
排行榜包含两个变量：
- _tree：二叉树，用于存储玩家排名信息
- _userMap：字典，用于存储玩家ID到玩家对象的映射

这里就不详细展开了，主要是添加、更新、删除玩家时需要维护这两个数据结构。具体实现可以参考完整代码。


#### 完整代码
```csharp
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
                    UserBucket = new UserBucket(new User[UserBucket.BucketSize], 0),
                }
                : BuildTree(0, buckets.Length, 1, maxDepth, buckets);
            _root.Color = ColorEnum.Black;
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

        /// <summary>
        /// 从排行榜中删除玩家
        /// </summary>
        /// <param name="user">要删除的玩家</param>
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
}
```

## 3.6 双向跳表设计
双向跳表是一种结合了链表和二分查找优点的数据结构，通过在链表节点中维护多层索引，实现了高效的查找、插入和删除操作。本设计采用分桶技术与双向跳表相结合的方式，进一步优化了空间利用率和操作性能。

```csharp
public class BucketBiSkipListRankingList : IRankingList
{
    private BiSkipList _userList;
    private Dictionary<int, User> _userMap;

    public BucketBiSkipListRankingList(Span<User> users)
    {
        users.Sort();
        _userList = new BiSkipList(users);

        _userMap = new(users.Length);
        foreach (ref readonly User u in users)
        {
            _userMap[u.Id] = u;
        }
    }

    public BucketBiSkipListRankingList(List<User> users) :
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

    // 参考：https://cloud.tencent.com/developer/article/2512982（不正确，level不对）
    // 参考：https://www.baeldung-cn.com/java-skiplist
    // 源码：https://github.com/tedcy/algorithm_test/blob/master/order_set/t_zset.h

    /// <summary>
    /// 3.6.1 跳表节点：数据定义
    /// BiSkipListNode是双向跳表的基本构成单元，每个节点包含以下核心组件：
    /// 1. SkipListLevel结构：定义了节点在每一层的连接信息
    ///    - Next：指向下一个节点的引用
    ///    - Previous：指向前一个节点的引用
    ///    - PreviousCount：到前一个节点的用户数量
    /// 2. UserBucket：存储该节点管理的用户数据桶
    /// 3. Level数组：维护节点在各层的连接信息
    /// 4. MinUser：冗余存储桶内最小用户，优化查询性能
    /// </summary>
    class BiSkipListNode
    {
        public struct SkipListLevel
        {
            public BiSkipListNode? Next;
            public BiSkipListNode? Previous;
            public int PreviousCount; // 到前一个节点的用户数量（不包含本节点的用户数量）
        }
        public UserBucket UserBucket;
        public SkipListLevel[] Level;
        // 优化内存局部性，冗余存储每个节点的最小用户，避免访问UserBucket时的指针跳转
        public User MinUser;
        public BiSkipListNode(UserBucket bucket, int level)
        {
            UserBucket = bucket;
            Level = new SkipListLevel[level];
            MinUser = bucket.MinUser;
        }
    }

    /// <summary>
    /// 3.6.2 跳表设计：
    /// BiSkipList是双向跳表的核心实现类，负责管理跳表的节点结构和提供各种操作方法。
    /// 主要特性包括：
    /// 1. 支持双向遍历，可从任意节点向前或向后查找
    /// 2. 采用分桶技术存储用户数据，减少节点数量
    /// 3. 维护多层索引，实现O(log n)时间复杂度的查找、插入和删除
    /// 4. 支持动态扩容和缩容，根据实际数据量调整结构
    /// </summary>
    class BiSkipList
    {
        private const int MaxLevel = 32; // 跳表的最大层数
        private const double P = 0.25; // 跳表的概率
        public BiSkipListNode Head;
        public int Count;
        private Random _random = new();
        private int _level = 1;

        public BiSkipList(Span<User> initialUsers)
        {
            UserBucket[] buckets = BuildBucket(initialUsers);
            if (buckets.Length == 0)
            {
                // 没有用户
                UserBucket userBucket = new(new User[UserBucket.BucketSize], 0);
                Head = new BiSkipListNode(userBucket, MaxLevel);
                return;
            }
            else
            {
                Head = new BiSkipListNode(buckets[0], MaxLevel);
                BuildSkipList(buckets.AsSpan(1));
            }
            Count = initialUsers.Length;
        }

        /// <summary>
        /// 将初始用户数据构建为多个用户桶
        /// </summary>
        /// <param name="users">排序后的用户数据</param>
        /// <returns>构建好的用户桶数组</returns>
        /// <remarks>
        /// 该方法将排序后的用户数据划分为多个大小均匀的桶，每个桶的初始大小由UserBucket.InitialBucketSize决定。
        /// 这样可以减少跳表节点的数量，提高内存利用率和查询效率。
        /// </remarks>
        private static UserBucket[] BuildBucket(Span<User> users)
        {
            // 初始化Bucket
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

        /// <summary>
        /// 根据用户桶构建跳表结构
        /// </summary>
        /// <param name="buckets">用户桶数组</param>
        /// <remarks>
        /// 该方法负责构建跳表的多层索引结构：
        /// 1. 为每个桶创建一个跳表节点
        /// 2. 随机生成每个节点的层数
        /// 3. 建立各层节点之间的前后连接关系
        /// 4. 维护每个节点到前一个节点的用户数量
        /// 5. 调整跳表的实际层数
        /// </remarks>
        private void BuildSkipList(Span<UserBucket> buckets)
        {
            // 构建跳表
            int[] userCount = new int[MaxLevel];
            BiSkipListNode[] currentLevelNodes = new BiSkipListNode[MaxLevel];
            for (int i = 0; i < MaxLevel; i++)
            {
                userCount[i] = Head.UserBucket.UserCount;
                currentLevelNodes[i] = Head;
            }
            foreach (var bucket in buckets)
            {
                int randomLevel = RandomLevel();
                BiSkipListNode newNode = new(bucket, randomLevel);
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
                    userCount[i] += bucket.UserCount;
                }
            }
            _level = MaxLevel;
            while (_level > 1 && Head.Level[_level - 1].Next == null)
            {
                _level--;
            }
        }

        /// <summary>
        /// 随机生成节点的层数
        /// </summary>
        /// <returns>节点的随机层数</returns>
        /// <remarks>
        /// 该方法使用几何分布随机生成节点的层数，概率参数为P=0.25。
        /// 这样可以确保跳表的结构平衡，维持O(log n)的时间复杂度。
        /// </remarks>
        private int RandomLevel()
        {
            int level = 1;
            while (_random.NextDouble() < P && level < MaxLevel)
            {
                level++;
            }
            return level;
        }

        /// <summary>
        /// 向跳表中添加一个新用户
        /// </summary>
        /// <param name="user">要添加的用户</param>
        /// <returns>用户的排名</returns>
        /// <remarks>
        /// 该方法实现了高效的用户添加操作：
        /// 1. 从最高层开始查找，定位到用户应该插入的位置
        /// 2. 维护各层节点的用户数量信息
        /// 3. 如果当前桶未满，直接将用户插入到桶中
        /// 4. 如果当前桶已满，将桶分裂并创建新的跳表节点
        /// 5. 为新节点随机生成层数，并更新各层的连接关系
        /// 6. 返回用户的排名（从0开始）
        /// </remarks>
        public int AddUser(User user)
        {
            int rankCount = 0;
            int[] userCount = new int[MaxLevel];
            BiSkipListNode[] update = new BiSkipListNode[MaxLevel];
            BiSkipListNode current = Head;
            for (int i = _level - 1; i >= 0; i--)
            {
                while (current.Level[i].Next != null &&
                    current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                {
                    current = current.Level[i].Next!;
                    userCount[i] += current.Level[i].PreviousCount;
                }
                rankCount += userCount[i];
                // 增加区间用户数量
                if (current.Level[i].Next != null)
                {
                    current.Level[i].Next!.Level[i].PreviousCount++;
                }
                update[i] = current;
            }

            int userIndexInBucket;
            UserBucket userBucket = current.UserBucket;
            if (!userBucket.Full)
            {
                userIndexInBucket = userBucket.Insert(user);
                if (userIndexInBucket == 0)
                {
                    current.MinUser = user;
                }
            }
            else
            {
                UserBucket newBucket = userBucket.Split(user, out userIndexInBucket);
                if (userIndexInBucket == 0)
                {
                    current.MinUser = user;
                }

                int randomLevel = RandomLevel();
                if (randomLevel > _level)
                {
                    for (int i = _level; i < randomLevel; i++)
                    {
                        update[i] = Head;
                    }
                    _level = randomLevel;
                }
                BiSkipListNode newNode = new(newBucket, randomLevel);
                int previousCount = userBucket.UserCount;
                for (int i = 0; i < randomLevel; i++)
                {
                    newNode.Level[i].Next = update[i].Level[i].Next;
                    update[i].Level[i].Next = newNode;
                    newNode.Level[i].Previous = update[i];
                    newNode.Level[i].PreviousCount = previousCount;
                    if (newNode.Level[i].Next != null)
                    {
                        newNode.Level[i].Next!.Level[i].PreviousCount -= previousCount;
                        newNode.Level[i].Next!.Level[i].Previous = newNode;
                    }
                    previousCount += userCount[i];
                }
            }

            Count++;

            return rankCount + userIndexInBucket;
        }

        /// <summary>
        /// 从跳表中删除一个用户
        /// </summary>
        /// <param name="user">要删除的用户</param>
        /// <remarks>
        /// 该方法实现了高效的用户删除操作：
        /// 1. 从最高层开始查找，定位到包含该用户的桶
        /// 2. 维护各层节点的用户数量信息
        /// 3. 从桶中删除用户
        /// 4. 根据桶的状态决定是否需要合并或删除节点：
        ///    - 如果桶为空，删除该节点
        ///    - 如果桶的用户数量过少且前一个桶也不满，合并两个桶并删除当前节点
        /// 5. 更新跳表的层数和连接关系
        /// </remarks>
        public void RemoveUser(User user)
        {
            int[] userCount = new int[_level];
            BiSkipListNode current = Head;
            for (int i = _level - 1; i >= 0; i--)
            {
                while (current.Level[i].Next != null
                    && current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                {
                    current = current.Level[i].Next!;
                    userCount[i] += current.Level[i].PreviousCount;
                }
                // 减少区间用户数量
                if (current.Level[i].Next != null)
                {
                    current.Level[i].Next!.Level[i].PreviousCount--;
                }
            }

            UserBucket userBucket = current.UserBucket;
            int userIndexInBucket = userBucket.Remove(user);
            bool needDelete = false;
            if (userBucket.Empty)
            {
                needDelete = true;
            }
            else if (current.UserBucket.UserCount < UserBucket.CombineBucketSize
                        && current.Level[0].Previous?.UserBucket.UserCount < UserBucket.CombineBucketSize)
            {
                current.Level[0].Previous!.UserBucket.Combine(current.UserBucket);
                needDelete = true;
            }
            if (!needDelete)
            {
                if (userIndexInBucket == 0)
                {
                    current.MinUser = userBucket.MinUser;
                }
            }
            else
            {
                // Head节点不删除，保留一个空的桶
                if (current != Head)
                {
                    for (int i = 0; i < current.Level.Length; i++)
                    {
                        current.Level[i].Previous!.Level[i].Next = current.Level[i].Next;
                        if (current.Level[i].Next != null)
                        {
                            current.Level[i].Next!.Level[i].PreviousCount += current.Level[i].PreviousCount;
                            current.Level[i].Next!.Level[i].Previous = current.Level[i].Previous;
                        }
                    }
                    while (_level > 1 && Head.Level[_level - 1].Next == null)
                    {
                        _level--;
                    }
                }
            }
            Count--;
        }

        /// <summary>
        /// 获取指定用户的排名
        /// </summary>
        /// <param name="user">要查询的用户</param>
        /// <returns>用户的排名（从0开始）</returns>
        /// <remarks>
        /// 该方法通过多层索引快速定位用户位置：
        /// 1. 从最高层开始查找，跳过不可能包含该用户的区间
        /// 2. 累计经过的用户数量，计算排名
        /// 3. 在找到的桶中精确定位用户位置
        /// 4. 返回总排名 = 前面所有桶的用户数量 + 桶内排名
        /// </remarks>
        public int GetUserRank(User user)
        {
            int rankCount = 0;
            BiSkipListNode current = Head;
            for (int i = _level - 1; i >= 0; i--)
            {
                while (current.Level[i].Next != null
                    && current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                {
                    current = current.Level[i].Next!;
                    rankCount += current.Level[i].PreviousCount;
                }
            }
            UserBucket userBucket = current.UserBucket;
            int userIndexInBucket = userBucket.IndexOf(user);
            Debug.Assert(userIndexInBucket >= 0, "用户不存在");
            return rankCount + userIndexInBucket;
        }

        /// <summary>
        /// 获取排行榜前N名用户
        /// </summary>
        /// <param name="topN">要获取的用户数量</param>
        /// <returns>前N名用户的数组</returns>
        /// <remarks>
        /// 该方法高效获取排行榜顶部用户：
        /// 1. 从跳表头部开始遍历
        /// 2. 依次从每个桶中获取用户数据
        /// 3. 使用数组拷贝优化性能
        /// 4. 自动处理topN超过总用户数的情况
        /// </remarks>
        public User[] GetTopN(int topN)
        {
            topN = Math.Min(topN, Count);
            User[] result = new User[topN];
            BiSkipListNode? current = Head;
            int rankCount = 0;
            while (rankCount < topN)
            {
                Debug.Assert(current != null);
                int n = Math.Min(current.UserBucket.UserCount, topN - rankCount);
                Array.Copy(current.UserBucket.Users, 0, result, rankCount, n);
                rankCount += n;
                current = current.Level[0].Next;
            }
            return result;
        }

        /// <summary>
        /// 获取指定用户周围的用户列表
        /// </summary>
        /// <param name="user">中心用户</param>
        /// <param name="aroundN">周围的用户数量（左边和右边各aroundN个）</param>
        /// <returns>包含周围用户的数组和中心用户的排名</returns>
        /// <remarks>
        /// 该方法实现了高效的范围查询：
        /// 1. 快速定位中心用户的位置
        /// 2. 从中心用户所在桶开始，向左右扩展
        /// 3. 处理边界情况（排名过前或过后）
        /// 4. 使用双向链表的特性高效遍历前后节点
        /// 5. 返回结果数组和中心用户的排名
        /// </remarks>
        public (User[], int) GetAroundUser(User user, int aroundN)
        {
            // 1. 找到对应的位置
            int rankCount = 0;
            BiSkipListNode current = Head;
            for (int i = _level - 1; i >= 0; i--)
            {
                while (current.Level[i].Next != null
                    && current.Level[i].Next!.MinUser.CompareTo(user) <= 0)
                {
                    current = current.Level[i].Next!;
                    rankCount += current.Level[i].PreviousCount;
                }
            }
            UserBucket userBucket = current.UserBucket;
            int userIndexInBucket = userBucket.IndexOf(user);
            Debug.Assert(userIndexInBucket >= 0, "用户不存在");
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
            if (rankCount + aroundN + 1 > Count)
            {
                // 用户排名过靠后，无法获取足够的右边用户
                rightNum = Count - rankCount - 1;
            }
            User[] result = new User[leftNum + rightNum + 1];

            // 3. 把桶内的用户填充到结果数组中
            // 左边计数
            int leftCount = Math.Min(userIndexInBucket, leftNum);
            // 右边计数
            int rightCount = Math.Min(userBucket.UserCount - userIndexInBucket - 1, rightNum);
            Array.Copy(userBucket.Users, userIndexInBucket - leftCount, result, aroundN - leftCount + offset,
                leftCount + rightCount + 1);

            // 4. 获取缺少的用户
            BiSkipListNode tNode = current.Level[0].Previous!;
            while (leftCount < leftNum)
            {
                userBucket = tNode!.UserBucket!;
                int n = Math.Min(userBucket.UserCount, leftNum - leftCount);
                Array.Copy(userBucket.Users, userBucket.UserCount - n, result, aroundN - leftCount - n + offset, n);
                leftCount += n;
                tNode = tNode.Level[0].Previous;
            }
            tNode = current.Level[0].Next!;
            while (rightCount < rightNum)
            {
                userBucket = tNode!.UserBucket!;
                int n = Math.Min(userBucket.UserCount, rightNum - rightCount);
                Array.Copy(userBucket.Users, 0, result, aroundN + rightCount + 1 + offset, n);
                rightCount += n;
                tNode = tNode.Level[0].Next;
            }
            return (result, rankCount);
        }
    }
}
```

# 四、性能测试

## 4.1 测试环境

- **CPU**: AMD Ryzen 9 9700X
- **内存**: 64GB DDR5
- **操作系统**: Windows 11
- **运行时**: .NET 10.0
- **测试数据量**: 10万用户、10万次操作（混合测试为100万用户、100万次操作）

## 4.2 主要实现版本

| 数据结构 | 实现方案 |
|---------|------|
|  有序列表 | 使用`List<User>`实现，增删改查使用二分查找 |
| 分桶 + 列表 | 使用`List<UserBucket>`实现列表管理桶，每个桶内使用`List<User>`实现用户列表。增删改查以以桶的最大值来判断是否存在桶内，桶内采用二分查找 |
| 分桶 + 链表 | 使用`LinkedList<UserBucket>`实现列表管理桶 |
| 分桶 + 双向跳表 | 与红黑树方案对比的备选方案 |
| 分桶 + 单向跳表 | 简化版的跳表实现 |
| 纯红黑树 | 无分桶的红黑树方案 |
| 分桶 + 红黑树 | 本文重点介绍的高性能方案 |


## 4.2 测试数据

测试数据采用幂律分布生成100万个用户，分数范围为0到1000000，越高的分数用户越少，更符合真实游戏场景中的玩家分数分布规律。用户分数分布图如下：
![用户分数分布图](./PowerDistribution.png)

测试以下几个操作，执行100万次操作：
- **Add**：添加新用户到排行榜
- **Update**：更新现有用户的分数
- **GetRank**：查询指定用户的当前排名
- **GetTopN**：获取排行榜前N名玩家
- **GetAround**：获取指定玩家周围的排名情况
- **混合测试（100万用户）**：模拟真实场景下的混合操作
- **混合测试（1000万用户，执行1000万次操作）**：测试系统在大规模数据下的表现

其中，用户分数更新采用在原分数基础上增加0-100分的方式，增加的分数同样符合幂律分布。混合测试的操作概率分布为：
- Add: 10%
- Update: 20%
- GetRank: 30%
- GetTopN: 20%
- GetAround: 20%


## 4.3 测试结果

### 表格1：耗时对比

| 实现 | Add | Update | GetRank | GetTopN | GetAround | 混合测试 <br />(100w用户) | 混合测试 <br />(1000w用户，1000w操作) |
|------|---------|------------|---------|-------------|---------------|---------------------|----------------------|
| **有序数组** | 40474 ms <br />(+20341.41%↑) | 455636 ms <br />(+75720.63%↑) | 59 ms <br />(+22.92%↑) | 319 ms <br />(+1.59%↑) | 438 ms <br />(+6.05%↑) | 97719 ms <br />(+23390.14%↑) | - |
| **分桶** | 23620 ms <br />(+11829.29%↑) | 14809 ms <br />(+2364.06%↑) | 43 ms <br />(-10.42%↓) | 7572 ms <br />(+2311.46%↑) | 7627 ms <br />(+1746.73%↑) | 8488 ms <br />(+1940.38%↑) | - |
| **分桶 + 链表** | 56436 ms <br />(+28403.03%↑) | 22588 ms <br />(+3658.40%↑) | 41 ms <br />(-14.58%↓) | 12714 ms <br />(+3949.04%↑) | 12147 ms <br />(+2841.16%↑) | 13152 ms <br />(+3061.54%↑) | - |
| **分桶 + 双向跳表** | 287 ms <br />(+44.95%↑) | 728 ms <br />(+21.13%↑) | **40 ms <br />(-16.67%↓)** | 344 ms <br />(+9.55%↑) | 521 ms <br />(+26.15%↑) | 514 ms <br />(+23.56%↑) | 7380 ms <br />(+5.64%↑) |
| **分桶 + 单向跳表** | 288 ms <br />(+45.45%↑) | 760 ms <br />(+26.46%↑) | **40 ms <br />(-16.67%↓)** | 416 ms <br />(+32.48%↑) | 620 ms <br />(+50.12%↑) | 538 ms <br />(+29.33%↑) | 8293 ms <br />(+18.71%↑) |
| **纯红黑树** | 368 ms <br />(+85.86%↑) | 1788 ms <br />(+197.50%↑) | 199 ms <br />(+314.58%↑) | 595 ms <br />(+89.49%↑) | 1431 ms <br />(+246.49%↑) | 1427 ms <br />(+243.03%↑) | 20104 ms <br />(+187.78%↑) |
| **分桶 + 红黑树** | **198 ms** (基准) | **601 ms** (基准) | 48 ms (基准) | **314 ms** (基准) | **413 ms** (基准) | **416 ms** (基准) | **6986 ms** (基准) |

> **说明**：加粗项为各列最优值。↑ 表示比基准差（耗时更长），↓ 表示比基准优（耗时更短）。数值越小越好。

### 表格2：内存占用对比

| 实现 | Add | Update | GetRank | GetTopN | GetAround | 混合测试 (100w用户) | 混合测试 (1000w用户) |
|------|---------|------------|---------|-------------|---------------|---------------------|----------------------|
| **有序数组** | **113.29 MB <br />(-22.97%↓)** | **55.17 MB <br />(-23.56%↓)** | **55.18 MB <br />(-23.54%↓)** | **55.18 MB <br />(-23.54%↓)** | **55.18 MB <br />(-23.54%↓)** | **70.43 MB <br />(-6.55%↓)** | - |
| **分桶** | 144.57 MB <br />(-1.70%↓) | 70.92 MB <br />(-1.73%↓) | 70.91 MB <br />(-1.74%↓) | 70.92 MB <br />(-1.72%↓) | 70.92 MB <br />(-1.72%↓) | 74.05 MB <br />(-1.75%↓) | - |
| **分桶 + 链表** | 145.15 MB <br />(-1.30%↓) | 71.22 MB <br />(-1.32%↓) | 71.21 MB <br />(-1.32%↓) | 71.22 MB <br />(-1.31%↓) | 71.22 MB <br />(-1.31%↓) | 74.32 MB <br />(-1.39%↓) | - |
| **分桶 + 双向跳表** | 146.00 MB <br />(-0.73%↓) | 71.64 MB <br />(-0.74%↓) | 71.63 MB <br />(-0.75%↓) | 71.63 MB <br />(-0.75%↓) | 71.63 MB <br />(-0.75%↓) | 74.78 MB <br />(-0.78%↓) | **1038.63 MB** (-0.57%↓) |
| **分桶 + 单向跳表** | 146.08 MB <br />(-0.67%↓) | 71.68 MB <br />(-0.68%↓) | 71.67 MB <br />(-0.69%↓) | 71.67 MB <br />(-0.69%↓) | 71.67 MB <br />(-0.69%↓) | 74.83 MB <br />(-0.72%↓) | **1039.07 MB <br />(-0.53%↓)** |
| **纯红黑树** | 387.95 MB <br />(+163.79%↑) | 192.51 MB <br />(+166.75%↑) | 192.51 MB <br />(+166.75%↑) | 192.51 MB <br />(+166.76%↑) | 192.51 MB <br />(+166.76%↑) | 207.79 MB <br />(+175.68%↑) | 2365.34 MB <br />(+126.44%↑) |
| **分桶 + 红黑树** | 147.07 MB <br />(基准) | 72.17 MB (基准) | 72.17 MB (基准) | 72.17 MB (基准) | 72.17 MB (基准) | 75.37 MB (基准) | 1044.58 MB (基准) |

> **说明**：加粗项为各列最优值。↑ 表示比基准差（内存更多），↓ 表示比基准优（内存更少）。数值越小越好。

## 4.5 测试结果分析

测试结果不出意外，分桶以后的耗时和内存占用都比有序数组要好。但是有几个地方需要注意：
- 分桶 + 列表的效率大于分桶 + 链表

## 4.5.1 分桶和有序数组对比

通过表格一和表格二，我们可以看到分桶以后虽然增加了内存占用，但是耗时大幅度减少。
原因在于，分桶以后，每个桶的用户数量更少，所以定位桶的时间更短。同时，每个桶的有序数组更小，所以批量复制的时间更短。

## 4.5.2 分桶和链表对比

以1万用户，100万操作的测试案例为例：

| 实现 | Add | Update | GetRank | GetTopN | GetAround | 混合测试 |
|------|---------|------------|---------|-------------|---------------|----------|
| BucketListRankingList | 13058 ms | 1707 ms | 51 ms | 787 ms | 988 ms | 1688 ms |
| BucketLinkedListRankingList | 39800 ms | 4135 ms | 49 ms | 2084 ms | 1297 ms | 2377 ms |

List<UserBucket> 的 连续内存结构 带来更高的 CPU 缓存命中率，所以耗时更短。

## 4.5.3 红黑树和双向跳表对比

AMDuProf测试显示：
test BucketBRTreeListRankingList -t 02-t100w_100 L1_DC_MISS_RATIO 0.009
test BucketBiSkipListRankingList -t 02-t100w_100 L1_DC_MISS_RATIO 0.017
L1数据缓冲占所有L1缓存访问的比例，数值越小表示内存局部性越好。
BucketSkipListRankingList3的L1数据缓冲命中率较低，可能是因为跳表节点的内存分布较为分散，导致CPU缓存效率较低。
相比之下，BucketBRTreeListRankingList的内存布局可能更有利于缓存，从而表现出更好的性能。

# 六、总结与展望

## 6.1 设计要点回顾

本项目设计了一个基于**分桶 + 红黑树**混合数据结构的高性能游戏全服排行榜系统，核心设计要点包括：

1. **分桶策略**：将玩家按分数范围划分为固定大小的桶（默认256个玩家/桶），桶内采用有序数组存储，充分利用连续内存的缓存友好特性
2. **红黑树管理**：使用红黑树高效管理所有桶，利用区间信息快速定位目标桶，时间复杂度稳定在O(log M)
3. **桶的动态调整**：实现了桶的自动分裂和合并机制，当桶满时自动分裂，当桶内玩家过少时自动合并，保持系统效率
4. **区间与计数缓存**：树节点缓存子树的区间信息和用户计数，加速排名计算和范围查询

## 6.2 性能表现

系统在各种操作场景下都表现出优异的性能：

| 操作 | 时间复杂度 | 实际表现 |
|-----|-----------|--------|
| 添加用户 | O(log M + K) | 百万级用户下平均耗时约198ms/百万次 |
| 更新用户 | O(log M + K) | 百万级用户下平均耗时约601ms/百万次 |
| 获取排名 | O(log M + log K) | 百万级用户下平均耗时约48ms/百万次 |
| 获取前N名 | O(N + log M) | 百万级用户下平均耗时约314ms/百万次 |
| 获取周围玩家 | O(log M + log K + 2N) | 百万级用户下平均耗时约413ms/百万次 |

（注：M为桶数量，K为桶大小（256），N为获取数量）

## 6.3 后续优化方向

虽然当前方案已经表现出色，但仍有一些可以进一步优化的方向：

1. **自适应桶大小**：根据实际负载动态调整桶的大小，在不同压力下保持最优性能
2. **并发支持**：当前实现为单线程版本，可以考虑添加并发控制机制，支持多线程操作
3. **持久化方案**：实现排行榜数据的持久化存储和恢复机制，提高系统可靠性
4. **分布式扩展**：设计分布式排行榜架构，支持超大规模用户（亿级）的场景
5. **更多排序维度**：支持多维度排序（如分数+等级+活跃度等复合排序规则）

## 6.4 适用场景

本实现特别适合以下场景：
- 游戏全服排行榜（战力榜、等级榜、竞技场榜等）
- 社交应用排行榜（影响力榜、贡献榜等）
- 需要高并发、低延迟的实时排名系统
- 数据规模在百万到千万级别的应用

通过分桶和红黑树的巧妙结合，本方案在内存使用效率、操作延迟和系统稳定性之间取得了良好的平衡，为高性能排行榜系统提供了一个可靠的解决方案。

# 七、参考资料

- [一文带你彻底读懂红黑树 - 知乎](https://zhuanlan.zhihu.com/p/91960960)
- [红黑树详解 - 博客园](https://www.cnblogs.com/crazymakercircle/p/16320430.html)
- [B+树详解 - 维基百科](https://zh.wikipedia.org/wiki/B%2B%E6%A0%91)
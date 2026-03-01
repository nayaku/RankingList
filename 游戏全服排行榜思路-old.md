首先需要定义一个排行榜，一个游戏的排行榜应该包含以下操作：

- 添加玩家
- 更新玩家分数
- 获取排行榜前N名玩家
- 获取某个玩家的排名
- 获取玩家周围的排名
- 获得玩家总数

```csharp
    public interface IRankingList
    {
        int AddUser(User user);
        int UpdateUser(User user);
        int GetUserRank(int userId);
        List<User> GetTopN(int topN);
        (List<User>, int) GetAroundUser(int userId, int aroundN);
        int GetRankingCount();
    }
```

排行榜的排序根据玩家的分数和获取时间排序，分数高的玩家优先，相同分数的玩家根据获取时间排序，先获取的玩家优先。

```csharp
    public readonly struct User : IComparable<User>, IEquatable<User>
    {
        public readonly int Id;
        public readonly int Score;
        public readonly DateTime LastActive;

        public int CompareTo(User other)
        {
            if (Score == other.Score)
                return -LastActive.CompareTo(other.LastActive);
            else if (LastActive == other.LastActive)
                return -Id.CompareTo(other.Id);
            return -Score.CompareTo(other.Score);
        }

        ... // 此处省略 Equals, GetHashCode, 运算符重载等方法
    }
```

这里采用了结构体来表示玩家，因为玩家的信息不会改变，所以采用结构体可以避免频繁的内存分配。

测试时，主要测试添加、更新玩家分数、获取排行榜前N名玩家、获取某个玩家的排名和获取玩家周围的排名。不同操作有不同的频率：
- 添加玩家：10%
- 更新玩家分数：20%
- 获取排行榜前N名玩家：30%
- 获取某个玩家的排名：20%
- 获取玩家周围的排名：20%
具体测试代码：TODO 链接

## 一、 最简单的实现

直接采用 `List` 来存储玩家，操作也使用最简单的，添加、更新和删除后重排序，查询采用遍历。

```csharp


```
用户数: 0
操作数: 997
总耗时: 3 ms
平均耗时: 3.01 ms/1000操作

用户数: 10
操作数: 10000
总耗时: 147 ms
平均耗时: 14.70 ms/1000操作

## 二、 简单优化后的排行榜

采用二分查找优化添加、更新和查询操作。

更新时，根据旧的用户数据二分查找，删除指定位置的数据，然后插入到新的位置。

类定义：
```csharp
public class BListRankingList : IRankingList
{
    private readonly List<User> _users;
    private readonly Dictionary<int, User> _usersDict;
}
```

`_usersDict` 用于查找玩家ID对应的用户数据，`_users` 用于存储玩家有序数据。

添加用户：

```csharp
public int AddUser(User user)
{
    int insertIndex = _users.BinarySearch(user);
    if (insertIndex < 0)
    {
        insertIndex = ~insertIndex;
    }

    _users.Insert(insertIndex, user);
    _usersDict[user.Id] = user;
    return insertIndex;
}
```

更新用户：先查找旧用户，然后删除旧用户，最后插入新用户。

```csharp
public int UpdateUser(User user)
{
    // 移除旧用户
    int oldIndex = GetUserRank(user.Id);
    _users.RemoveAt(oldIndex);
    // 插入新用户
    int insertIndex = AddUser(user);
    return insertIndex;
}
```

查找用户排名：
```csharp
public int GetUserRank(int userId)
{
    User user = _usersDict[userId];
    int index = _users.BinarySearch(user);
    Debug.Assert(index >= 0);
    Debug.Assert(_users[index].Id == userId);
    return index;
}
```
获取排行榜前N名玩家：
```csharp
public List<User> GetTopN(int topN)
{
    return [.. _users.Take(topN)];
}
```

获取玩家周围的排名：
```csharp
public (List<User>, int) GetAroundUser(int userId, int aroundN)
{
    int rank = GetUserRank(userId);
    int start = Math.Max(0, rank - aroundN);
    int end = Math.Min(_users.Count - 1, rank + aroundN);
    int count = end - start + 1;
    List<User> result = _users.GetRange(start, count);
    return (result, rank);
}
```


用户数: 0
操作数: 997
排行榜用户数: 113
总耗时: 3 ms vs 4 ms (-25.00%)
平均耗时: 3.01 ms/1k操作 vs 4.01 ms/1k操作 (-25.00%)

用户数: 10
操作数: 10000
总耗时: 8 ms vs 141 ms (-94.33%)
平均耗时: 0.80 ms/1k操作 vs 14.10 ms/1k操作 (-94.33%)


## 三、 基于桶排序的排行榜

直接基于数组的排行榜，在插入和删除的时，需要移动剩下的玩家数据，耗时过大。可以基于桶排序的思想，分成多个桶，每个桶内采用有序数组存储玩家，桶与桶之间也采用有序数组存储。当桶内玩家过多的时候，需要切分为两个桶；当桶内玩家过少的时候，需要合并桶。

定义桶：
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

    ...
}
```

向桶插入玩家：
```csharp
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
```

移除桶内玩家：
```csharp
public void Remove(User user)
{
    int index = Array.BinarySearch(Users, 0, UserCount, user);
    Debug.Assert(index >= 0);

    Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
    UserCount--;
}
```
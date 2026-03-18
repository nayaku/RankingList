using System.Diagnostics;

namespace RankingListNew
{
    /// <summary>
    /// 用户桶
    /// 桶内玩家按分数有序排列，使用有序数组实现
    /// </summary>
    internal class UserBucket
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
        /// 合并桶
        /// </summary>
        /// <param name="other"></param>
        public void Combine(UserBucket other)
        {
            Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
            UserCount += other.UserCount;
        }
    }
}

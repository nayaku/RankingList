using System.Diagnostics;

namespace RankingListNew
{
    /// <summary>
    /// 每个桶
    /// </summary>
    public class UserBucket
    {
        public const int BucketSize = 256; // 每个bucket的用户数量
        public const int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量

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
            if (index < Users.Length)
            {
                Array.Copy(Users, index, Users, index + 1, UserCount - index);
            }
            Users[index] = user;
            UserCount++;
            return index;
        }

        public int Remove(User user)
        {
            int index = Array.BinarySearch(Users, 0, UserCount, user);
            Debug.Assert(index >= 0, "用户不存在");
            UserCount--;
            if (index < UserCount)
            {
                Array.Copy(Users, index + 1, Users, index, UserCount - index);
            }

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
    }
}

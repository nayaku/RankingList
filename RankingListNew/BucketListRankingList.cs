using System.Diagnostics;

namespace RankingListNew
{
    public class BucketListRankingList : IRankingList
    {
        private const int BucketSize = 512; // 每个桶包含的玩家数
        private const int InitialBucketSize = BucketSize / 2; // 初始桶大小
        private int _userCount;
        private List<UserBucket> _buckets;
        private Dictionary<int, User> _userDict;
#if DEBUG
        private int _splitCount;
        private int _combineCount;
#endif
        public BucketListRankingList(List<User> users)
        {
            users.Sort();
            int bucketNum = (int)Math.Ceiling((double)users.Count / InitialBucketSize);
            // 初始化每个桶
            _buckets = new List<UserBucket>(bucketNum);
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * InitialBucketSize;
                int r = Math.Min((i + 1) * InitialBucketSize, users.Count);
                int userCount = r - l;
                User[] bucketUsers = new User[BucketSize];
                users.CopyTo(l, bucketUsers, 0, userCount);
                _buckets.Add(new UserBucket(bucketUsers, userCount));
            }

            _userDict = users.ToDictionary(u => u.Id, u => u);
            _userCount = users.Count;
        }

        public int AddUser(User user)
        {
            int rankCount = 0;
            if (_userCount == 0)
            {
                User[] bucketUsers = new User[BucketSize];
                bucketUsers[0] = user;
                _buckets.Add(new UserBucket(bucketUsers, 1));
            }
            else
            {
                int bucketIndex;
                int userIndexInBucket;
                for (bucketIndex = 0; bucketIndex < _buckets.Count - 1; bucketIndex++)
                // 找不到就选择最后一个bucket
                {
                    if (user.CompareTo(_buckets[bucketIndex].MaxUser) <= 0)
                    {
                        break;
                    }

                    rankCount += _buckets[bucketIndex].UserCount;
                }

                if (_buckets[bucketIndex].Full)
                {
                    // 分裂bucket
                    UserBucket newBucket = _buckets[bucketIndex].Split(user, out userIndexInBucket);
                    _buckets.Insert(bucketIndex + 1, newBucket);
#if DEBUG
                    _splitCount++;
#endif
                }
                else
                {
                    // 加入bucket
                    userIndexInBucket = _buckets[bucketIndex].Insert(user);
                }
                rankCount += userIndexInBucket;
            }
            _userDict[user.Id] = user;
            _userCount++;
            return rankCount;
        }

        private void RemoveUser(User user)
        {
            int bucketIndex;
            for (bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
            {
                UserBucket bucket = _buckets[bucketIndex];
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    bucket.Remove(user);
                    break;
                }
            }

            Debug.Assert(bucketIndex < _buckets.Count, "用户不存在");
            if (_buckets[bucketIndex].Empty)
            {
                _buckets.RemoveAt(bucketIndex);
            }
            else if (_buckets[bucketIndex].UserCount < BucketSize / 4 && bucketIndex != 0 &&
                     _buckets[bucketIndex - 1].UserCount < BucketSize / 4)
            {
                // 向前合并
                _buckets[bucketIndex - 1].Combine(_buckets[bucketIndex]);
                _buckets.RemoveAt(bucketIndex);
#if DEBUG
                _combineCount++;
#endif
            }

            _userCount--;
        }

        public int UpdateUser(User user)
        {
            User oldUser = _userDict[user.Id];
            RemoveUser(oldUser);
            return AddUser(user);
        }

        public int GetUserRank(int userId)
        {
            int rankCount = 0;
            User user = _userDict[userId];
            foreach (UserBucket bucket in _buckets)
            {
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    int rankInBucket = bucket.IndexOf(user);
                    Debug.Assert(rankInBucket >= 0);
                    rankCount += rankInBucket;
                    break;
                }

                rankCount += bucket.UserCount;
            }

            return rankCount;
        }

        public User[] GetTopN(int topN)
        {
            int rankCount = 0;
            List<User> result = new(topN);
            for (int bucketIndex = 0; bucketIndex < _buckets.Count && rankCount < topN; bucketIndex++)
            {
                UserBucket bucket = _buckets[bucketIndex];
                for (int inBucketIndex = 0; inBucketIndex < bucket.UserCount && rankCount < topN; inBucketIndex++)
                {
                    result.Add(bucket.Users[inBucketIndex]);
                    rankCount++;
                }
            }

            return result.ToArray();
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            int rankCount = 0;
            int bucketIndex = -1;
            User user = _userDict[userId];
            for (int i = 0; i < _buckets.Count; i++)
            {
                if (user.CompareTo(_buckets[i].MaxUser) <= 0)
                {
                    bucketIndex = i;
                    break;
                }

                rankCount += _buckets[i].UserCount;
            }

            Debug.Assert(bucketIndex != -1);

            int inBucketIndex = _buckets[bucketIndex].IndexOf(user);
            Debug.Assert(inBucketIndex != -1);
            int resultRank = rankCount + inBucketIndex;
            int startRank = Math.Max(0, resultRank - aroundN);
            int endRank = Math.Min(resultRank + aroundN, _userCount - 1);
            int count = endRank - startRank + 1;

            for (; rankCount > startRank; bucketIndex--)
            {
                rankCount -= _buckets[bucketIndex - 1].UserCount;
            }

            inBucketIndex = startRank - rankCount;
            List<User> result = new(count);
            for (int resultIndex = 0; resultIndex < count; bucketIndex++)
            {
                UserBucket bucket = _buckets[bucketIndex];
                for (; inBucketIndex < bucket.UserCount && resultIndex < count; inBucketIndex++)
                {
                    result.Add(bucket.Users[inBucketIndex]);
                    resultIndex++;
                }
                inBucketIndex = 0;
            }

            return (result.ToArray(), resultRank);
        }

        public int GetRankingCount()
        {
            return _userCount;
        }

#if DEBUG
        public void DebugPrint()
        {
            Console.WriteLine($"UserCount: {_userCount}");
            Console.Write("Each Bucket Number of Users: ");
            for (int i = 0; i < _buckets.Count; i++)
            {
                Console.Write($"{_buckets[i].UserCount} ");
            }

            Console.WriteLine();
            Console.WriteLine("Each Bucket Score Range:");
            for (int i = 0; i < _buckets.Count; i++)
            {
                Console.WriteLine(
                    $"Bucket {i}: {(_buckets[i].MinUser).Score} - {(_buckets[i].MaxUser).Score}");
            }

            Console.WriteLine($"SplitCount: {_splitCount}");
            Console.WriteLine($"CombineCount: {_combineCount}");
        }
#endif

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

            public void Remove(User user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                Debug.Assert(index >= 0);

                Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
                UserCount--;
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
                Dictionary<int, User> newUserDict = new(BucketSize);
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
                Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
                UserCount += other.UserCount;
            }
        }
    }
}

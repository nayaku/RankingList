using System.Diagnostics;

namespace RankingListNew
{
    public class BucketLinkedListRankingList : IRankingList
    {
        private const int BucketSize = 256; // 每个桶包含的玩家数
        private const int InitialBucketSize = BucketSize / 2; // 初始桶大小
        private int _userCount;
        private LinkedList<UserBucket> _buckets;
        private Dictionary<int, User> _userDict;
#if DEBUG
        private int _splitCount;
        private int _combineCount;
#endif
        public BucketLinkedListRankingList(List<User> users)
        {
            users.Sort();
            int bucketNum = (int)Math.Ceiling((double)users.Count / InitialBucketSize);
            // 初始化每个桶
            _buckets = new();
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * InitialBucketSize;
                int r = Math.Min((i + 1) * InitialBucketSize, users.Count);
                int userCount = r - l;
                User[] bucketUsers = new User[BucketSize];
                users.CopyTo(l, bucketUsers, 0, userCount);
                _buckets.AddLast(new UserBucket(bucketUsers, userCount));
            }
            if (users.Count == 0)
            {
                User[] bucketUsers = new User[BucketSize];
                _buckets.AddLast(new UserBucket(bucketUsers, 0));
            }
            _userDict = users.ToDictionary(u => u.Id, u => u);
            _userCount = users.Count;
        }

        public int AddUser(User user)
        {
            int rankCount = 0;
            if (_userCount == 0)
            {
                _buckets.Last!.Value.Insert(user);
            }
            else
            {
                LinkedListNode<UserBucket> bucketNode = _buckets.First!;
                int userIndexInBucket;
                while (bucketNode.Next != null)
                // 找不到就选择最后一个bucket
                {
                    if (user.CompareTo(bucketNode.Value.MaxUser) <= 0)
                    {
                        break;
                    }
                    rankCount += bucketNode.Value.UserCount;
                    bucketNode = bucketNode.Next!;
                }

                if (bucketNode.Value.Full)
                {
                    // 分裂bucket
                    UserBucket newBucket = bucketNode.Value.Split(user, out userIndexInBucket);
                    _buckets.AddAfter(bucketNode, newBucket);
#if DEBUG
                    _splitCount++;
#endif
                }
                else
                {
                    // 加入bucket
                    userIndexInBucket = bucketNode.Value.Insert(user);
                }
                rankCount += userIndexInBucket;
            }
            _userDict[user.Id] = user;
            _userCount++;
            return rankCount;
        }

        private void RemoveUser(User user)
        {
            LinkedListNode<UserBucket> bucketNode = _buckets.First!;
            while (bucketNode != null)
            {
                UserBucket bucket = bucketNode.Value;
                if (user.CompareTo(bucket.MaxUser) <= 0)
                {
                    bucket.Remove(user);
                    break;
                }
                bucketNode = bucketNode.Next!;
            }

            Debug.Assert(bucketNode != null, "用户不存在");
            if (_userCount == 1)
            { }
            else if (bucketNode.Value.Empty)
            {
                _buckets.Remove(bucketNode);
            }
            else if (bucketNode.Value.UserCount < BucketSize / 4 && bucketNode.Previous != null &&
                     bucketNode.Previous.Value.UserCount < BucketSize / 4)
            {
                // 向前合并
                bucketNode.Previous.Value.Combine(bucketNode.Value);
                _buckets.Remove(bucketNode);
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
            topN = Math.Min(topN, _userCount);
            User[] result = new User[topN];
            LinkedListNode<UserBucket>? bucketNode = _buckets.First;
            while (rankCount < topN)
            {
                UserBucket bucket = bucketNode.Value;
                int n = Math.Min(bucket.UserCount, topN - rankCount);
                Array.Copy(bucket.Users, 0, result, rankCount, n);
                rankCount += n;
                bucketNode = bucketNode.Next;
            }

            return result;
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            int rankCount = 0;
            User user = _userDict[userId];

            LinkedListNode<UserBucket> bucketNode = _buckets.First!;
            while (bucketNode != null)
            {
                if (user.CompareTo(bucketNode.Value.MaxUser) <= 0)
                {
                    break;
                }
                rankCount += bucketNode.Value.UserCount;
                bucketNode = bucketNode.Next;
            }
            Debug.Assert(bucketNode != null);
            int inBucketIndex = bucketNode.Value.IndexOf(user);
            Debug.Assert(inBucketIndex != -1);
            int resultRank = rankCount + inBucketIndex;
            int startRank = Math.Max(0, resultRank - aroundN);
            int endRank = Math.Min(resultRank + aroundN, _userCount - 1);
            int count = endRank - startRank + 1;

            while (rankCount > startRank)
            {
                bucketNode = bucketNode.Previous!;
                rankCount -= bucketNode.Value.UserCount;
            }

            inBucketIndex = startRank - rankCount;
            User[] result = new User[count];
            for (int resultIndex = 0; resultIndex < count;)
            {
                UserBucket bucket = bucketNode.Value;
                int n = Math.Min(bucket.UserCount - inBucketIndex, count - resultIndex);
                Array.Copy(bucket.Users, inBucketIndex, result, resultIndex, n);
                resultIndex += n;
                inBucketIndex = 0;
                bucketNode = bucketNode.Next!;
            }

            return (result, resultRank);
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
            for (LinkedListNode<UserBucket>? bucketNode = _buckets.First; bucketNode != null; bucketNode = bucketNode.Next)
            {
                Console.Write($"{bucketNode.Value.UserCount} ");
            }

            Console.WriteLine();
            Console.WriteLine("Each Bucket Score Range:");
            for (LinkedListNode<UserBucket>? bucketNode = _buckets.First; bucketNode != null; bucketNode = bucketNode.Next)
            {
                Console.WriteLine(
                    $"Bucket {(bucketNode.Value.MinUser).Score} - {(bucketNode.Value.MaxUser).Score}");
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

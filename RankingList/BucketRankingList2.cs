using System.Diagnostics;

namespace RankingList
{
    public class BucketRankingList2 : IRankingList
    {
        private static readonly int BucketSize = 512; // Define the score range for each bucket
        private static readonly int InitialBucketSize = BucketSize / 2; // Initial size for each bucket
        private int _userCount;
        private List<UserBucket> _buckets;

        public BucketRankingList2(IUser[] users)
        {
            Array.Sort(users);
            int bucketNum = (int)Math.Ceiling((double)users.Length / InitialBucketSize);
            // Initialize buckets and distribute users into buckets
            _buckets = new List<UserBucket>(bucketNum);
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * InitialBucketSize;
                int r = Math.Min((i + 1) * InitialBucketSize, users.Length);
                int userCount = r - l;
                IUser[] bucketUsers = new IUser[BucketSize];
                Array.Copy(users, l, bucketUsers, 0, userCount);
                Dictionary<int, IUser> userIds = bucketUsers[..userCount].ToDictionary(u => u.Id, u => u);

                _buckets.Add(new UserBucket(bucketUsers, userCount, userIds));
            }

            _userCount = users.Length;
        }

        public RankingListResponse AddUser(IUser user)
        {
            int bucketIndex;
            int userIndexInBucket;
            for (bucketIndex = _buckets.Count - 1; bucketIndex > 0; bucketIndex--) // 找不到就选第一个，所以第一个不用比
            {
                var bucket = _buckets[bucketIndex];
                if (user.CompareTo(bucket.MinUser) >= 0)
                {
                    break;
                }
            }

            if (_buckets[bucketIndex].Full)
            {
                // 分裂bucket
                var newBucket = _buckets[bucketIndex].Split(user, out userIndexInBucket);
                _buckets.Insert(bucketIndex + 1, newBucket);
            }
            else
            {
                // 加入bucket
                userIndexInBucket = _buckets[bucketIndex].Insert(user);
            }

            int rankCount = 0;
            for (int i = 0; i < bucketIndex; i++)
            {
                rankCount += _buckets[i].UserCount;
            }

            rankCount += userIndexInBucket + 1;
            _userCount++;
            return new RankingListResponse
            {
                User = user,
                Rank = rankCount
            };
        }

        private void RemoveUser(IUser user)
        {
            int bucketIndex;
            for (bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
            {
                var bucket = _buckets[bucketIndex];
                if (!bucket.UserIds.TryGetValue(user.Id, out IUser? existingUser)) continue;
                bucket.Remove(existingUser);
                break;
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
                _buckets[bucketIndex - 1].Combine([_buckets[bucketIndex]]);
                _buckets.RemoveAt(bucketIndex);
            }

            _userCount--;
        }

        public RankingListResponse UpdateUser(IUser user)
        {
            RemoveUser(user);
            return AddUser(user);
        }

        public RankingListResponse GetUserRank(int userId)
        {
            int rankCount = 0;
            for (int bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
            {
                var bucket = _buckets[bucketIndex];
                if (bucket.UserIds.TryGetValue(userId, out IUser? user))
                {
                    int index = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, user);
                    Debug.Assert(index >= 0);
                    return new RankingListResponse
                    {
                        User = bucket.Users[index],
                        Rank = rankCount + index + 1
                    };
                }

                rankCount += bucket.UserCount;
            }

            return new RankingListResponse
            {
                User = null,
                Rank = -1
            };
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            var result = new RankingListResponse[topN];
            int rankCount = 0;
            for (int bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
            {
                var bucket = _buckets[bucketIndex];
                int count = Math.Min(topN - rankCount, bucket.UserCount);
                for (int i = 0; i < count; i++)
                {
                    result[rankCount + i] = new RankingListResponse
                    {
                        User = bucket.Users[i],
                        Rank = rankCount + i + 1
                    };
                }

                rankCount += count;
            }

            return result;
        }

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            int rankCount = 0;
            int bucketIndex = -1;
            IUser? user = null;
            for (int i = 0; i < _buckets.Count; i++)
            {
                if (_buckets[i].UserIds.TryGetValue(userId, out user))
                {
                    bucketIndex = i;
                    break;
                }

                rankCount += _buckets[i].UserCount;
            }
            Debug.Assert(user != null);

            int inBucketIndex = Array.BinarySearch(_buckets[bucketIndex].Users, 0, _buckets[bucketIndex].UserCount, user);
            Debug.Assert(inBucketIndex != -1);
            int startRank = Math.Max(0, rankCount + inBucketIndex - aroundN);
            int endRank = Math.Min(rankCount + inBucketIndex + aroundN, _userCount - 1);
            int count = endRank - startRank + 1;

            for (; rankCount > startRank && bucketIndex > 0; bucketIndex--)
            {
                rankCount -= _buckets[bucketIndex - 1].UserCount;
            }

            inBucketIndex = startRank - rankCount;
            RankingListResponse[] result = new RankingListResponse[count];
            int resultIndex = 0;
            for (; bucketIndex < _buckets.Count; bucketIndex++)
            {
                var bucket = _buckets[bucketIndex];
                for (; inBucketIndex < bucket.UserCount && rankCount + inBucketIndex <= endRank; inBucketIndex++)
                {
                    result[resultIndex++] = new RankingListResponse
                    {
                        User = bucket.Users[inBucketIndex],
                        Rank = rankCount + inBucketIndex + 1
                    };
                }

                if (rankCount + inBucketIndex > endRank)
                {
                    break;
                }

                rankCount += bucket.UserCount;
                inBucketIndex = 0;
            }

            return result;
        }

        public int GetRankingCount()
        {
            return _userCount;
        }

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
                    $"Bucket {i}: {((User)_buckets[i].MinUser).Score} - {((User)_buckets[i].MaxUser).Score}");
            }
        }

        /// <summary>
        /// 每个桶
        /// </summary>
        internal class UserBucket
        {
            public IUser MinUser => Users[0];
            public IUser MaxUser => Users[UserCount - 1];
            public IUser[] Users { get; set; }
            public int UserCount { get; set; }
            public Dictionary<int, IUser> UserIds { get; set; }
            public bool Full => UserCount >= Users.Length;
            public bool Empty => UserCount == 0;

            public UserBucket(IUser[] users, int userCount, Dictionary<int, IUser> userIds)
            {
                Users = users;
                UserCount = userCount;
                UserIds = userIds;
            }

            public int Insert(IUser user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                if (index < 0)
                {
                    index = ~index;
                }

                Array.Copy(Users, index, Users, index + 1, UserCount - index);
                Users[index] = user;
                UserCount++;
                UserIds.Add(user.Id, user);
                return index;
            }

            public void Remove(IUser user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                Debug.Assert(index >= 0);

                Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
                Users[UserCount - 1] = null;
                UserIds.Remove(user.Id);
                UserCount--;
            }

            /// <summary>
            /// 分裂成两个桶
            /// </summary>
            /// <param name="user"></param>
            /// <param name="userIndex"></param>
            /// <returns>右边的新桶</returns>
            public UserBucket Split(IUser user, out int userIndex)
            {
                int mid = UserCount / 2;
                userIndex = Array.BinarySearch(Users, 0, UserCount, user);
                if (userIndex < 0)
                {
                    userIndex = ~userIndex;
                }

                IUser[] newUsers = new IUser[BucketSize];
                Dictionary<int, IUser> newUserIds = new(BucketSize);
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

                for (int i = 0; i < newUserCount; i++)
                {
                    newUserIds.Add(newUsers[i].Id, newUsers[i]);
                }

                for (int i = mid; i < UserCount; i++)
                {
                    UserIds.Remove(Users[i].Id);
                }

                Array.Clear(Users, mid, UserCount - mid);

                UserCount = mid;
                UserBucket newBucket = new(newUsers, newUserCount, newUserIds);
                if (userIndex < mid)
                    Insert(user);
                return newBucket;
            }

            public void Combine(UserBucket[] other)
            {
                foreach (var bucket in other)
                {
                    Array.Copy(bucket.Users, 0, Users, UserCount, bucket.UserCount);
                    UserCount += bucket.UserCount;
                    foreach (var user in bucket.UserIds.Values)
                    {
                        UserIds.Add(user.Id, user);
                    }
                }
            }
        }
    }
}
/*
=== 排行榜测试框架 ===

=== 测试 BucketRankingList2 排行榜 ===
总操作数: 300000
初始用户数: 10000

=== 验证操作结果与基准对比 ===
√ 所有操作结果验证通过！
测试操作结果已保存到 BucketRankingList2_test_results.json

=== 测试结果 ===
排行榜名称: BucketRankingList2
总耗时: 739 ms
平均耗时: 0.00 ms/操作
内存占用: 260.00 MB
内存峰值: 288.54 MB
测试日期: 2026/1/16 15:57:54

=== 与基准 SimpleRankingList2 的对比 ===
总耗时: 739 ms vs 8517 ms (-91.32%)
平均耗时: 0.00 ms vs 0.03 ms (-91.32%)
内存占用: 260.00 MB vs 259.20 MB (+0.31%)
内存峰值: 288.54 MB vs 288.79 MB (-0.08%)
*/
//namespace RankingList
//{
//    internal class UserBucket
//    {
//        public User MinUser { get; set; }
//        public User MaxUser { get; set; }
//        public User[] Users { get; set; }
//        public int UserCount { get; set; }
//        public HashSet<int> UserIds { get; set; }
//        public UserBucket(User minUser, User maxUser, User[] users, int userCount, HashSet<int> userIds)
//        {
//            MinUser = minUser;
//            MaxUser = maxUser;
//            Users = users;
//            UserIds = userIds;
//            UserCount = userCount;
//        }
//    }

//    internal class BucketRankingList : IRankingList
//    {
//        private const int BucketSize = 1000; // Define the score range for each bucket
//        private const int InitialBucketSize = 500; // Initial size for each bucket
//        private List<UserBucket> _buckets;
//        public BucketRankingList(User[] users)
//        {
//            users.Sort();
//            int bucketNum = users.Length / InitialBucketSize;
//            // Initialize buckets and distribute users into buckets
//            _buckets = new List<UserBucket>(bucketNum);
//            for (int i = 0; i < bucketNum; i++)
//            {
//                int l = i * InitialBucketSize;
//                int r = Math.Min((i + 1) * InitialBucketSize - 1, users.Length - 1);
//                int userCount = r - l + 1;
//                User minUser = users[l];
//                User maxUser = users[r];
//                User[] bucketUsers = new User[userCount];
//                Array.Copy(users, l, bucketUsers, 0, userCount);
//                HashSet<int> userIds = [.. bucketUsers.Select(user => user.ID)];
//                _buckets.Add(new UserBucket(minUser, maxUser, bucketUsers, userCount, userIds));
//            }
//        }
//        public void AddOrUpdateUser(int userId, int score, DateTime lastActive)
//        {
//            throw new NotImplementedException();
//        }

//        public RankingListMutiResponse GetRankingListMutiResponse(int topN, int aroundUserId, int aroundN)
//        {
//            throw new NotImplementedException();
//        }

//        public RankingListSingleResponse GetUserRank(int userId)
//        {
//            int rank = 1;
//            for (int b = 0; b < _buckets.Count; b++)
//            {
//                UserBucket bucket = _buckets[b];
//                if (bucket.UserIds.Contains(userId))
//                {
//                    for (int i = 0; i < bucket.UserCount; i++)
//                    {
//                        if (bucket.Users[i].ID == userId)
//                        {
//                            return new RankingListSingleResponse
//                            {
//                                User = bucket.Users[i],
//                                Rank = rank + i
//                            };
//                        }
//                    }
//                }
//                else
//                {
//                    rank += bucket.UserCount;
//                }
//            }
//            return new RankingListSingleResponse
//            {
//                User = null,
//                Rank = -1
//            };
//        }
//    }
//}

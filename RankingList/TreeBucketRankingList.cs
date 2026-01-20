using System.Diagnostics;

namespace RankingList
{
    public class TreeBucketRankingList : IRankingList
    {
        private static readonly int BucketSize = 16; // 每个bucket的用户数量
        private static readonly int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量
        private TreeNode _root;
        private Dictionary<int, IUser> _userMap;

        public TreeBucketRankingList(IUser[] users)
        {
            Array.Sort(users);
            UserBucket[] buckets = BuildBucket(users);
            _root = BuildTree(0, buckets.Length, buckets);
            _userMap = users.ToDictionary(u => u.Id, u => u);
        }

        private static UserBucket[] BuildBucket(IUser[] users)
        {
            // 初始化bucket
            int bucketNum = (int)Math.Ceiling((double)users.Length / InitialBucketSize);
            UserBucket[] buckets = new UserBucket[bucketNum];
            for (int i = 0; i < bucketNum; i++)
            {
                int l = i * InitialBucketSize;
                int r = Math.Min((i + 1) * InitialBucketSize, users.Length);
                int userCount = r - l;
                IUser[] bucketUsers = new IUser[BucketSize];
                Array.Copy(users, l, bucketUsers, 0, userCount);

                buckets[i] = new UserBucket(bucketUsers, userCount);
            }

            return buckets;
        }

        private static TreeNode BuildTree(int l, int r, UserBucket[] buckets)
        {
            // 初始化tree
            TreeNode node = new();
            if (l + 1 == r)
            {
                node.Count = buckets[l].UserCount;
                node.UserBucket = buckets[l];
                node.LeftUser = buckets[l].MinUser;
                node.RightUser = buckets[l].MaxUser;
                return node;
            }

            int mid = (l + r) >> 1;
            node.Left = BuildTree(l, mid, buckets);
            node.LeftUser = node.Left.LeftUser;
            node.Right = BuildTree(mid, r, buckets);
            node.RightUser = node.Right.RightUser;
            node.Count = node.Left.Count + node.Right.Count;
            return node;
        }

        private static void OperateTree(TreeNode node, IUser user, ref int rankCount, bool isAdd)
        {
            // 叶子节点
            if (node.UserBucket != null)
            {
                if (isAdd)
                {
                    int userIndexInBucket;
                    if (node.Full)
                    {
                        // 分裂TreeNode
                        node.Split(user, out userIndexInBucket);
                        rankCount += userIndexInBucket;
                        return;
                    }

                    // 加入bucket
                    userIndexInBucket = node.Insert(user);
                    rankCount += userIndexInBucket;
                }
                else
                {
                    node.Remove(user);
                }

                return;
            }

            // 非叶子节点，必定度为2
            Debug.Assert(node.Left != null && node.Right != null);
            if (user.CompareTo(node.Right.LeftUser) < 0)
            {
                OperateTree(node.Left, user, ref rankCount, isAdd);
                node.LeftUser = node.Left.LeftUser;
            }
            else
            {
                rankCount += node.Left.Count;
                OperateTree(node.Right, user, ref rankCount, isAdd);
                node.RightUser = node.Right.RightUser;
            }

            if (isAdd)
            {
                node.Count++;
            }
            else
            {
                node.Count--;
            }

            Debug.Assert(node.Count == node.Left.Count + node.Right.Count);
            if (node.UserBucket == null )
            {
                if (node.Left.Empty)
                {
                    // 左子树为空，用右子树代替
                    node.CopyFrom(node.Right);
                }
                else if (node.Right.Empty)
                {
                    // 右子树为空，用左子树代替
                    node.CopyFrom(node.Left);
                }
                else if (node.Count < BucketSize / 4)
                {
                    // 合并TreeNode
                    node.CombineChild();
                }
            }
        }

        public RankingListResponse AddUser(IUser user)
        {
            Debug.Assert(!_userMap.ContainsKey(user.Id));
            _userMap.Add(user.Id, user);
            int rankCount = 0;
            OperateTree(_root, user, ref rankCount, true);
            return new RankingListResponse
            {
                User = user,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse UpdateUser(IUser user)
        {
            int rankCount = 0;
            IUser oldUser = _userMap[user.Id];
            OperateTree(_root, oldUser, ref rankCount, false);
            rankCount = 0;
            OperateTree(_root, user, ref rankCount, true);
            _userMap[user.Id] = user;
            return new RankingListResponse
            {
                User = user,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse GetUserRank(int userId)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            IUser user = _userMap[userId];
            int rankCount = 0;
            TreeNode node = _root;

            while (node.UserBucket == null)
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

            UserBucket bucket = node.UserBucket;
            int userIndexInBucket = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, user);
            Debug.Assert(userIndexInBucket >= 0);
            rankCount += userIndexInBucket;
            return new RankingListResponse
            {
                User = user,
                Rank = rankCount + 1
            };
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            RankingListResponse[] result = new RankingListResponse[topN];
            int rankCount = 0;
            GetTopN(_root, topN, ref rankCount, ref result);
            return result;
        }

        private static void GetTopN(TreeNode node, int topN, ref int rankCount, ref RankingListResponse[] result)
        {
            if (node.UserBucket != null)
            {
                for (int i = 0; i < node.UserBucket.UserCount && rankCount < topN; i++, rankCount++)
                {
                    result[rankCount] = new RankingListResponse
                    {
                        User = node.UserBucket.Users[i],
                        Rank = rankCount + 1
                    };
                }

                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            GetTopN(node.Left, topN, ref rankCount, ref result);
            if (rankCount < topN)
            {
                GetTopN(node.Right, topN, ref rankCount, ref result);
            }
        }

        // 先获取用户在树中的排名，再获取左右aroundN个用户
        private static void GetAroundUserStep1(TreeNode node, IUser user, int aroundN, ref int rankCount,
            ref int leftCount, ref int rightCount, ref RankingListResponse[] result)
        {
            if (node.UserBucket != null)
            {
                UserBucket bucket = node.UserBucket;
                int userIndexInBucket = Array.BinarySearch(bucket.Users, 0, bucket.UserCount, user);
                Debug.Assert(userIndexInBucket >= 0);
                rankCount += userIndexInBucket;
                result[aroundN] = new RankingListResponse
                {
                    User = bucket.Users[userIndexInBucket],
                    Rank = rankCount + 1
                };
                // 左边
                for (int i = userIndexInBucket - 1; i >= 0 && leftCount < aroundN; i--, leftCount++)
                {
                    result[aroundN - leftCount - 1] = new RankingListResponse
                    {
                        User = bucket.Users[i],
                        Rank = rankCount - (leftCount + 1) + 1
                    };
                }

                // 右边
                for (int i = userIndexInBucket + 1; i < bucket.UserCount && rightCount < aroundN; i++, rightCount++)
                {
                    result[aroundN + rightCount + 1] = new RankingListResponse
                    {
                        User = bucket.Users[i],
                        Rank = rankCount + (rightCount + 1) + 1
                    };
                }

                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            if (user.CompareTo(node.Right.LeftUser) < 0)
            {
                GetAroundUserStep1(node.Left, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
                // 找到用户后，进入第二阶段
                if (rightCount < aroundN)
                {
                    GetAroundUserStep2(node.Right, aroundN, false, rankCount, ref rightCount, ref result);
                }
            }
            else
            {
                rankCount += node.Left.Count;
                GetAroundUserStep1(node.Right, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
                // 找到用户后，进入第二阶段
                if (leftCount < aroundN)
                {
                    GetAroundUserStep2(node.Left, aroundN, true, rankCount, ref leftCount, ref result);
                }
            }
        }

        private static void GetAroundUserStep2(TreeNode node, int aroundN, bool isRequiredLeft, int rankCount,
            ref int requiredCount, ref RankingListResponse[] result)
        {
            if (node.UserBucket != null)
            {
                UserBucket bucket = node.UserBucket;
                if (isRequiredLeft)
                {
                    // 缺少左边的用户
                    for (int i = bucket.UserCount - 1; i >= 0 && requiredCount < aroundN; i--, requiredCount++)
                    {
                        result[aroundN - requiredCount - 1] = new RankingListResponse
                        {
                            User = bucket.Users[i],
                            Rank = rankCount - (requiredCount + 1) + 1
                        };
                    }
                }
                else
                {
                    // 缺少右边的用户
                    for (int i = 0; i < bucket.UserCount && requiredCount < aroundN; i++, requiredCount++)
                    {
                        result[aroundN + requiredCount + 1] = new RankingListResponse
                        {
                            User = bucket.Users[i],
                            Rank = rankCount + (requiredCount + 1) + 1
                        };
                    }
                }
                return;
            }

            Debug.Assert(node.Left != null && node.Right != null);
            TreeNode[] children = isRequiredLeft ? [node.Right, node.Left] : [node.Left, node.Right];
            foreach (TreeNode child in children)
            {
                GetAroundUserStep2(child, aroundN, isRequiredLeft, rankCount, ref requiredCount, ref result);
                if (requiredCount >= aroundN)
                {
                    break;
                }
            }
        }

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            Debug.Assert(_userMap.ContainsKey(userId));
            IUser user = _userMap[userId];
            int rankCount = 0;
            int leftCount = 0;
            int rightCount = 0;
            RankingListResponse[] result = new RankingListResponse[aroundN * 2 + 1];
            GetAroundUserStep1(_root, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
            if (leftCount < aroundN || rightCount < aroundN)
            {
                // 未填满
                RankingListResponse[] newResult = new RankingListResponse[leftCount + rightCount + 1];
                Array.Copy(result, aroundN - leftCount, newResult, 0, leftCount + rightCount + 1);
                result = newResult;
            }
            return result;
        }

        public int GetRankingCount()
        {
            return _root.Count;
        }

        /// <summary>
        /// 每个桶
        /// </summary>
        class UserBucket
        {
            public IUser MinUser => Users[0];
            public IUser MaxUser => Users[UserCount - 1];

            public IUser[] Users { get; }
            public int UserCount { get; private set; }
            public bool Full => UserCount >= Users.Length;
            public bool Empty => UserCount == 0;

            public UserBucket(IUser[] users, int userCount)
            {
                Users = users;
                UserCount = userCount;
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
                return index;
            }

            public int Remove(IUser user)
            {
                int index = Array.BinarySearch(Users, 0, UserCount, user);
                Debug.Assert(index >= 0);

                Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
                Users[UserCount - 1] = null;
                UserCount--;
                return index;
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
                HashSet<int> newUserIds = new(BucketSize);
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
                    newUserIds.Add(newUsers[i].Id);
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
                Debug.Assert(UserCount + other.UserCount <= Users.Length);
                Array.Copy(other.Users, 0, Users, UserCount, other.UserCount);
                UserCount += other.UserCount;
            }
        }

        class TreeNode
        {
            public int Count { get; set; }
            public IUser LeftUser { get; set; }
            public IUser RightUser { get; set; }
            public TreeNode? Left { get; set; }
            public TreeNode? Right { get; set; }
            public UserBucket? UserBucket { get; set; }
            public bool Full => Count >= BucketSize;
            public bool Empty => Count == 0;

            public void CopyFrom(TreeNode other)
            {
                Count = other.Count;
                LeftUser = other.LeftUser;
                RightUser = other.RightUser;
                Left = other.Left;
                Right = other.Right;
                UserBucket = other.UserBucket;
            }

            public int Insert(IUser user)
            {
                Debug.Assert(UserBucket != null);
                int userIndexInBucket = UserBucket.Insert(user);
                if (userIndexInBucket == 0)
                {
                    LeftUser = user;
                }
                else if (userIndexInBucket == UserBucket.UserCount - 1)
                {
                    RightUser = user;
                }

                Count++;
                return userIndexInBucket;
            }

            public void Remove(IUser user)
            {
                Debug.Assert(UserBucket != null);
                int userIndexInBucket = UserBucket.Remove(user);
                if (UserBucket.Empty)
                {
                    // LeftUser = null;
                    // RightUser = null;
                }
                else if (userIndexInBucket == 0)
                {
                    LeftUser = UserBucket.MinUser;
                }
                else if (userIndexInBucket == UserBucket.UserCount)
                {
                    RightUser = UserBucket.MaxUser;
                }

                Count--;
            }

            public void Split(IUser user, out int userIndexInBucket)
            {
                Debug.Assert(UserBucket != null);
                UserBucket newBucket = UserBucket.Split(user, out userIndexInBucket);
                Left = new TreeNode()
                {
                    UserBucket = UserBucket,
                    Count = UserBucket.UserCount,
                    LeftUser = UserBucket.MinUser,
                    RightUser = UserBucket.MaxUser
                };
                Right = new TreeNode()
                {
                    UserBucket = newBucket,
                    Count = newBucket.UserCount,
                    LeftUser = newBucket.MinUser,
                    RightUser = newBucket.MaxUser
                };
                UserBucket = null;
                Count = Left.Count + Right.Count;
            }

            public void CombineChild()
            {
                Debug.Assert(Left != null && Right != null);
                if (Left.UserBucket == null)
                {
                    Left.CombineChild();
                }
                if (Right.UserBucket == null)
                {
                    Right.CombineChild();
                }
                Debug.Assert(Left.UserBucket != null && Right.UserBucket != null);
                UserBucket = Left.UserBucket;
                UserBucket.Combine(Right.UserBucket);
                Debug.Assert(UserBucket.UserCount == Count);
                Debug.Assert(UserBucket.MinUser == LeftUser);
                Debug.Assert(UserBucket.MaxUser == RightUser);
                Left = null;
                Right = null;
            }
        }
    }
}

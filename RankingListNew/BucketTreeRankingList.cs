//using System.Diagnostics;
//using System.Runtime.InteropServices;

//namespace RankingListNew
//{
//    public class BucketTreeRankingList : IRankingList
//    {
//        private Tree _tree;
//        private Dictionary<int, User> _userMap;

//        public BucketTreeRankingList(List<User> users)
//        {
//            users.Sort();
//            _tree = new Tree(CollectionsMarshal.AsSpan(users));
//            _userMap = users.ToDictionary(u => u.Id, u => u);
//        }

//        public int AddUser(User user)
//        {
//            Debug.Assert(!_userMap.ContainsKey(user.Id));
//            _userMap.Add(user.Id, user);
//            int rankCount = _tree.AddUser(user);

//            return rankCount;
//        }

//        public int UpdateUser(User newUser)
//        {
//            User oldUser = _userMap[newUser.Id];
//            _tree.RemoveUser(oldUser);
//            int rankCount = _tree.AddUser(newUser);
//            _userMap[newUser.Id] = newUser;
//            return rankCount;
//        }

//        public int GetUserRank(int userId)
//        {
//            Debug.Assert(_userMap.ContainsKey(userId));
//            User user = _userMap[userId];
//            return _tree.GetUserRank(user);
//        }

//        public User[] GetTopN(int topN)
//        {
//            return _tree.GetTopN(topN);
//        }

//        public (User[], int) GetAroundUser(int userId, int aroundN)
//        {
//            Debug.Assert(_userMap.ContainsKey(userId));
//            User user = _userMap[userId];
//            return _tree.GetAroundUser(user, aroundN);
//        }

//        public int GetRankingCount()
//        {
//            return _tree.GetRankingCount();
//        }

//        public void DebugPrint()
//        {
//#if DEBUG
//            _tree.DebugPrint();
//#endif
//        }

//        class Tree
//        {
//            private TreeNode _root;

//            public Tree(List<User> users)
//            {
//                UserBucket[] buckets = BuildBucket(users);
//                // 没有用户
//                _root = users.Count == 0
//                    ? new TreeNode()
//                    {
//                        UserBucket = new UserBucket(new User[UserBucket.BucketSize], 0),
//                    }
//                    : BuildTree(0, buckets.Length, buckets);
//            }
//            private static UserBucket[] BuildBucket(List<User> users)
//            {
//                // 初始化bucket
//                int bucketNum = (int)Math.Ceiling((double)users.Count / UserBucket.InitialBucketSize);
//                UserBucket[] buckets = new UserBucket[bucketNum];
//                for (int i = 0; i < bucketNum; i++)
//                {
//                    int l = i * UserBucket.InitialBucketSize;
//                    int r = Math.Min((i + 1) * UserBucket.InitialBucketSize, users.Count);
//                    int userCount = r - l;
//                    User[] bucketUsers = new User[UserBucket.BucketSize];
//                    users.CopyTo(l, bucketUsers, 0, userCount);
//                    buckets[i] = new UserBucket(bucketUsers, userCount);
//                }

//                return buckets;
//            }

//            private static TreeNode BuildTree(int l, int r, UserBucket[] buckets)
//            {
//                // 初始化tree
//                TreeNode node = new();
//                if (l + 1 == r)
//                {
//                    node.Count = buckets[l].UserCount;
//                    node.UserBucket = buckets[l];
//                    node.LeftUser = buckets[l].MinUser;
//                    node.RightUser = buckets[l].MaxUser;
//                    return node;
//                }

//                int mid = (l + r) >> 1;
//                node.Left = BuildTree(l, mid, buckets);
//                node.LeftUser = node.Left.LeftUser;
//                node.Right = BuildTree(mid, r, buckets);
//                node.RightUser = node.Right.RightUser;
//                node.Count = node.Left.Count + node.Right.Count;
//                return node;
//            }

//            private static void AddUser(TreeNode node, User user, ref int rankCount)
//            {
//                // 叶子节点
//                if (node.UserBucket != null)
//                {
//                    int userIndexInBucket;
//                    if (node.Full)
//                    {
//                        // 分裂TreeNode
//                        node.Split(user, out userIndexInBucket);
//                        rankCount += userIndexInBucket;
//                        return;
//                    }

//                    // 加入bucket
//                    userIndexInBucket = node.Insert(user);
//                    rankCount += userIndexInBucket;

//                    return;
//                }

//                // 非叶子节点，必定度为2
//                Debug.Assert(node.Left != null && node.Right != null);
//                if (user.CompareTo(node.Right.LeftUser) < 0)
//                {
//                    AddUser(node.Left, user, ref rankCount);
//                    node.LeftUser = node.Left.LeftUser;
//                }
//                else
//                {
//                    rankCount += node.Left.Count;
//                    AddUser(node.Right, user, ref rankCount);
//                    node.RightUser = node.Right.RightUser;
//                }

//                node.Count++;
//            }

//            private static void RemoveUser(TreeNode node, User user)
//            {
//                // 叶子节点
//                if (node.UserBucket != null)
//                {
//                    node.Remove(user);
//                    return;
//                }

//                // 非叶子节点，必定度为2
//                Debug.Assert(node.Left != null && node.Right != null);
//                if (user.CompareTo(node.Right.LeftUser) < 0)
//                {
//                    RemoveUser(node.Left, user);
//                    node.LeftUser = node.Left.LeftUser;
//                }
//                else
//                {
//                    RemoveUser(node.Right, user);
//                    node.RightUser = node.Right.RightUser;
//                }
//                node.Count--;
//                Debug.Assert(node.Count == node.Left.Count + node.Right.Count);
//                if (node.Left.Empty)
//                {
//                    // 左子树为空，用右子树代替
//                    node.CopyFrom(node.Right);
//                }
//                else if (node.Right.Empty)
//                {
//                    // 右子树为空，用左子树代替
//                    node.CopyFrom(node.Left);
//                }
//                else if (node.Count < BucketSize / 4)
//                {
//                    // 合并TreeNode
//                    node.CombineChild();
//                }
//            }

//            public int AddUser(User user)
//            {
//                int rankCount = 0;
//                if (_root.Count == 0)
//                {
//                    UserBucket bucket = _root.UserBucket!;
//                    bucket.Users[0] = user;
//                    bucket.UserCount = 1;
//                    _root.Count = 1;
//                    _root.LeftUser = user;
//                    _root.RightUser = user;
//                }
//                else
//                {
//                    AddUser(_root, user, ref rankCount);
//                }
//                return rankCount;
//            }

//            //public int UpdateUser(User user)
//            //{
//            //    User oldUser = _userMap[user.Id];
//            //    RemoveUser(_root, oldUser);
//            //    int rankCount = 0;
//            //    AddUser(_root, user, ref rankCount);
//            //    _userMap[user.Id] = user;
//            //    return rankCount;
//            //}

//            public int GetUserRank(User user)
//            {
//                int rankCount = 0;
//                TreeNode node = _root;

//                while (node.UserBucket == null)
//                {
//                    Debug.Assert(node.Left != null && node.Right != null);
//                    if (user.CompareTo(node.Right.LeftUser) < 0)
//                    {
//                        node = node.Left;
//                    }
//                    else
//                    {
//                        rankCount += node.Left.Count;
//                        node = node.Right;
//                    }
//                }

//                UserBucket bucket = node.UserBucket;
//                int userIndexInBucket = bucket.IndexOf(user);
//                Debug.Assert(userIndexInBucket >= 0);
//                rankCount += userIndexInBucket;
//                return rankCount;
//            }

//            public User[] GetTopN(int topN)
//            {
//                topN = Math.Min(topN, _root.Count);
//                User[] result = new User[topN];
//                int rankCount = 0;
//                GetTopN(_root, topN, ref rankCount, ref result);
//                return result;
//            }

//            private static void GetTopN(TreeNode node, int topN, ref int rankCount, ref User[] result)
//            {
//                if (node.UserBucket != null)
//                {
//                    int n = Math.Min(node.UserBucket.UserCount, topN - rankCount);
//                    Array.Copy(node.UserBucket.Users, 0, result, rankCount, n);
//                    //for (int i = 0; i < node.UserBucket.UserCount && rankCount < topN; i++, rankCount++)
//                    //{
//                    //    result.Add(node.UserBucket.Users[i]);
//                    //}
//                    return;
//                }

//                Debug.Assert(node.Left != null && node.Right != null);
//                GetTopN(node.Left, topN, ref rankCount, ref result);
//                if (rankCount < topN)
//                {
//                    GetTopN(node.Right, topN, ref rankCount, ref result);
//                }
//            }

//            // 先获取用户在树中的排名，再获取左右aroundN个用户
//            private static void GetAroundUserStep1(TreeNode node, User user, int aroundN, ref int rankCount,
//                ref int leftCount, ref int rightCount, ref User[]? result)
//            {
//                if (node.UserBucket != null)
//                {
//                    UserBucket bucket = node.UserBucket;
//                    int userIndexInBucket = bucket.IndexOf(user);
//                    Debug.Assert(userIndexInBucket >= 0);
//                    rankCount += userIndexInBucket;
//                    // 左边
//                    leftCount = Math.Min(userIndexInBucket, aroundN);
//                    // 右边
//                    rightCount = Math.Min(bucket.UserCount - userIndexInBucket - 1, aroundN);
//                    Array.Copy(bucket.Users, userIndexInBucket - leftCount, result, aroundN - leftCount,
//                        leftCount + rightCount + 1);
//                    return;
//                }

//                Debug.Assert(node.Left != null && node.Right != null);
//                if (user.CompareTo(node.Right.LeftUser) < 0)
//                {
//                    GetAroundUserStep1(node.Left, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
//                    // 找到用户后，进入第二阶段
//                    if (rightCount < aroundN)
//                    {
//                        GetAroundUserStep2(node.Right, aroundN, false, ref rightCount, ref result);
//                    }
//                }
//                else
//                {
//                    rankCount += node.Left.Count;
//                    GetAroundUserStep1(node.Right, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
//                    // 找到用户后，进入第二阶段
//                    if (leftCount < aroundN)
//                    {
//                        GetAroundUserStep2(node.Left, aroundN, true, ref leftCount, ref result);
//                    }
//                }
//            }

//            private static void GetAroundUserStep2(TreeNode node, int aroundN, bool isRequiredLeft, ref int obtainedCount,
//                ref User[] result)
//            {
//                if (node.UserBucket != null)
//                {
//                    UserBucket bucket = node.UserBucket;
//                    int n = Math.Min(bucket.UserCount, aroundN - obtainedCount);
//                    if (isRequiredLeft)
//                    {
//                        // 缺少左边的用户
//                        Array.Copy(bucket.Users, bucket.UserCount - n, result, aroundN - obtainedCount - n, n);
//                    }
//                    else
//                    {
//                        // 缺少右边的用户
//                        Array.Copy(bucket.Users, 0, result, aroundN + obtainedCount + 1, n);
//                    }
//                    obtainedCount += n;
//                    return;
//                }

//                Debug.Assert(node.Left != null && node.Right != null);
//                TreeNode[] children = isRequiredLeft ? [node.Right, node.Left] : [node.Left, node.Right];
//                foreach (TreeNode child in children)
//                {
//                    GetAroundUserStep2(child, aroundN, isRequiredLeft, ref obtainedCount, ref result);
//                    if (obtainedCount >= aroundN)
//                    {
//                        break;
//                    }
//                }
//            }

//            public (User[], int) GetAroundUser(User user, int aroundN)
//            {
//                int rankCount = 0;
//                int leftCount = 0;
//                int rightCount = 0;
//                User[] result = null;// = new User[aroundN * 2 + 1];
//                GetAroundUserStep1(_root, user, aroundN, ref rankCount, ref leftCount, ref rightCount, ref result);
//                List<User> aroundUsers = new(leftCount + rightCount + 1);
//                for (int i = aroundN - leftCount; i < aroundN + rightCount + 1; i++)
//                {
//                    aroundUsers.Add(result[i]);
//                }
//                return (aroundUsers, rankCount);
//            }

//            public int GetRankingCount()
//            {
//                return _root.Count;
//            }

//#if DEBUG
//            public void DebugPrint()
//            {
//                List<(int depth, int count)> results = [];
//                DebugPrint(_root, 0, ref results);
//                for (int i = 0; i < results.Count; i++)
//                {
//                    Console.Write($"{results[i].depth}-{results[i].count}  ");
//                    // 每10个换行
//                    if ((i + 1) % 10 == 0)
//                    {
//                        Console.WriteLine();
//                    }
//                }
//            }

//            private void DebugPrint(TreeNode node, int depth, ref List<(int depth, int count)> results)
//            {
//                if (node.UserBucket != null)
//                {
//                    results.Add((depth, node.UserBucket.UserCount));
//                    return;
//                }

//                DebugPrint(node.Left!, depth + 1, ref results);
//                DebugPrint(node.Right!, depth + 1, ref results);
//            }
//        }
//#endif

//        class TreeNode
//        {
//            public int Count;
//            public User LeftUser;
//            public User RightUser;
//            public TreeNode? Left;
//            public TreeNode? Right;
//            public UserBucket? UserBucket;
//            public bool Full => Count >= UserBucket.BucketSize;
//            public bool Empty => Count == 0;

//            public void CopyFrom(TreeNode other)
//            {
//                Count = other.Count;
//                LeftUser = other.LeftUser;
//                RightUser = other.RightUser;
//                Left = other.Left;
//                Right = other.Right;
//                UserBucket = other.UserBucket;
//            }

//            public int Insert(User user)
//            {
//                Debug.Assert(UserBucket != null);
//                int userIndexInBucket = UserBucket.Insert(user);
//                if (userIndexInBucket == 0)
//                {
//                    LeftUser = user;
//                }
//                else if (userIndexInBucket == UserBucket.UserCount - 1)
//                {
//                    RightUser = user;
//                }

//                Count++;
//                return userIndexInBucket;
//            }

//            public void Remove(User user)
//            {
//                Debug.Assert(UserBucket != null);
//                int userIndexInBucket = UserBucket.Remove(user);
//                if (UserBucket.Empty)
//                {
//                    // LeftUser = null;
//                    // RightUser = null;
//                }
//                else if (userIndexInBucket == 0)
//                {
//                    LeftUser = UserBucket.MinUser;
//                }
//                else if (userIndexInBucket == UserBucket.UserCount)
//                {
//                    RightUser = UserBucket.MaxUser;
//                }

//                Count--;
//            }

//            public void Split(User user, out int userIndexInBucket)
//            {
//                Debug.Assert(UserBucket != null);
//                UserBucket newBucket = UserBucket.Split(user, out userIndexInBucket);
//                Left = new TreeNode()
//                {
//                    UserBucket = UserBucket,
//                    Count = UserBucket.UserCount,
//                    LeftUser = UserBucket.MinUser,
//                    RightUser = UserBucket.MaxUser
//                };
//                Right = new TreeNode()
//                {
//                    UserBucket = newBucket,
//                    Count = newBucket.UserCount,
//                    LeftUser = newBucket.MinUser,
//                    RightUser = newBucket.MaxUser
//                };
//                UserBucket = null;
//                Count = Left.Count + Right.Count;
//            }

//            public void CombineChild()
//            {
//                Debug.Assert(Left != null && Right != null);
//                if (Left.UserBucket == null)
//                {
//                    Left.CombineChild();
//                }

//                if (Right.UserBucket == null)
//                {
//                    Right.CombineChild();
//                }

//                Debug.Assert(Left.UserBucket != null && Right.UserBucket != null);
//                UserBucket = Left.UserBucket;
//                UserBucket.Combine(Right.UserBucket);
//                Debug.Assert(UserBucket.UserCount == Count);
//                Debug.Assert(UserBucket.MinUser.CompareTo(LeftUser) == 0);
//                Debug.Assert(UserBucket.MaxUser.CompareTo(RightUser) == 0);
//                Left = null;
//                Right = null;
//            }
//        }
//    }
//}

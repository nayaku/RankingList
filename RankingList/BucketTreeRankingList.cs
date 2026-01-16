// using System.Collections;
// using System.Diagnostics;

// namespace RankingList
// {
//     internal class BucketTreeRankingList : IRankingList
//     {
//         private static readonly int BucketSize = 16; // 每个bucket的用户数量
//         private static readonly int InitialBucketSize = BucketSize / 2; // 初始每个bucket的用户数量
//         private List<UserBucket> _buckets;
//         private TreeNode _root;
//         private Dictionary<int, IUser> _userMap;

//         private void BuildBucket(IUser[] users)
//         {
//             // 初始化bucket
//             int bucketNum = (int)Math.Ceiling((double)users.Length / InitialBucketSize);
//             _buckets = new List<UserBucket>(bucketNum);
//             for (int i = 0; i < bucketNum; i++)
//             {
//                 int l = i * InitialBucketSize;
//                 int r = Math.Min((i + 1) * InitialBucketSize, users.Length);
//                 int userCount = r - l;
//                 IUser[] bucketUsers = new IUser[BucketSize];
//                 Array.Copy(users, l, bucketUsers, 0, userCount);

//                 _buckets.Add(new UserBucket(bucketUsers, userCount));
//             }
//         }

//         private TreeNode BuildTree(int l, int r)
//         {
//             // 初始化tree
//             TreeNode node = new TreeNode();
//             if (l + 1 == r)
//             {
//                 node.Count = _buckets[l].UserCount;
//                 node.BucketIndex = l;
//                 node.LeftUser = _buckets[l].MinUser;
//                 node.RightUser = _buckets[l].MaxUser;
//                 return node;
//             }

//             int mid = (l + r) >> 1;
//             node.Left = BuildTree(l, mid);
//             node.LeftUser = node.Left.LeftUser;
//             node.Right = BuildTree(mid, r);
//             node.RightUser = node.Right.RightUser;
//             node.Count = node.Left.Count + node.Right.Count;
//             return node;
//         }

//         public BucketTreeRankingList(IUser[] users)
//         {
//             Array.Sort(users);
//             BuildBucket(users);
//             _root = BuildTree(0, _buckets.Count);
//             _userMap = users.ToDictionary(u => u.Id, u => u);
//         }

//         public RankingListResponse AddUser(IUser user)
//         {
//             int bucketIndex;
//             int userIndexInBucket;
//             for (bucketIndex = _buckets.Count - 1; bucketIndex > 0; bucketIndex--) // 找不到就选第一个，所以第一个不用比
//             {
//                 var bucket = _buckets[bucketIndex];
//                 if (user.CompareTo(bucket.MinUser) >= 0)
//                 {
//                     break;
//                 }
//             }

//             if (_buckets[bucketIndex].Full)
//             {
//                 // 分裂bucket
//                 var newBucket = _buckets[bucketIndex].Split(user, out userIndexInBucket);
//                 _buckets.Insert(bucketIndex + 1, newBucket);
//             }
//             else
//             {
//                 // 加入bucket
//                 userIndexInBucket = _buckets[bucketIndex].Insert(user);
//             }

//             int rankCount = 0;
//             for (int i = 0; i < bucketIndex; i++)
//             {
//                 rankCount += _buckets[i].UserCount;
//             }

//             rankCount += userIndexInBucket + 1;
//             _userCount++;
//             return new RankingListResponse
//             {
//                 User = user,
//                 Rank = rankCount
//             };
//         }

//         private void RemoveUser(IUser user)
//         {
//             int bucketIndex;
//             for (bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
//             {
//                 var bucket = _buckets[bucketIndex];
//                 if (!bucket.UserIds.Contains(user.Id)) continue;
//                 bucket.Remove(user);
//                 break;
//             }

//             Debug.Assert(bucketIndex < _buckets.Count, "用户不存在");
//             if (_buckets[bucketIndex].Empty)
//             {
//                 _buckets.RemoveAt(bucketIndex);
//             }
//             else if (_buckets[bucketIndex].UserCount < BucketSize / 4 && bucketIndex != 0 &&
//                      _buckets[bucketIndex - 1].UserCount < BucketSize / 4)
//             {
//                 // 向前合并
//                 _buckets[bucketIndex - 1].Combine([_buckets[bucketIndex]]);
//                 _buckets.RemoveAt(bucketIndex);
//             }

//             _userCount--;
//         }

//         public RankingListResponse UpdateUser(IUser user)
//         {
//             RemoveUser(user);
//             return AddUser(user);
//         }

//         public RankingListResponse GetUserRank(int userId)
//         {
//             if (!_userMap.TryGetValue(userId, out IUser user))
//             {
//                 return new RankingListResponse
//                 {
//                     User = null,
//                     Rank = -1
//                 };
//             }
//             TreeNode node = _root;

//             while (node.BucketIndex == -1)
//             {
//                 if (user.CompareTo(_buckets[node.BucketIndex].MinUser) < 0)
//                 {
//                     node = node.Left;
//                 }
//                 else
//                 {
//                     rankCount += node.Left.Count;
//                     node = node.Right;
//                 }
//             }

//             int rankCount = 0;
//             for (int bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
//             {
//                 var bucket = _buckets[bucketIndex];
//                 if (bucket.UserIds.Contains(userId))
//                 {
//                     int index = bucket.IndexOf(userId);
//                     return new RankingListResponse
//                     {
//                         User = bucket.Users[index],
//                         Rank = rankCount + index + 1
//                     };
//                 }

//                 rankCount += bucket.UserCount;
//             }

//             return new RankingListResponse
//             {
//                 User = null,
//                 Rank = -1
//             };
//         }

//         public RankingListResponse[] GetTopN(int topN)
//         {
//             var result = new RankingListResponse[topN];
//             int rankCount = 0;
//             for (int bucketIndex = 0; bucketIndex < _buckets.Count; bucketIndex++)
//             {
//                 var bucket = _buckets[bucketIndex];
//                 int count = Math.Min(topN - rankCount, bucket.UserCount);
//                 for (int i = 0; i < count; i++)
//                 {
//                     result[rankCount + i] = new RankingListResponse
//                     {
//                         User = bucket.Users[i],
//                         Rank = rankCount + i + 1
//                     };
//                 }

//                 rankCount += count;
//             }

//             return result;
//         }

//         public RankingListResponse[] GetAroundUser(int userId, int aroundN)
//         {
//             int rankCount = 0;
//             int bucketIndex = -1;
//             for (int i = 0; i < _buckets.Count; i++)
//             {
//                 if (_buckets[i].UserIds.Contains(userId))
//                 {
//                     bucketIndex = i;
//                     break;
//                 }

//                 rankCount += _buckets[i].UserCount;
//             }

//             if (bucketIndex == -1)
//             {
//                 return [];
//             }

//             int inBucketIndex = _buckets[bucketIndex].IndexOf(userId);
//             Debug.Assert(inBucketIndex != -1);
//             int startRank = Math.Max(0, rankCount + inBucketIndex - aroundN);
//             int endRank = Math.Min(rankCount + inBucketIndex + aroundN, _userCount - 1);
//             int count = endRank - startRank + 1;

//             for (; rankCount > startRank && bucketIndex > 0; bucketIndex--)
//             {
//                 rankCount -= _buckets[bucketIndex - 1].UserCount;
//             }

//             inBucketIndex = startRank - rankCount;
//             RankingListResponse[] result = new RankingListResponse[count];
//             int resultIndex = 0;
//             for (; bucketIndex < _buckets.Count; bucketIndex++)
//             {
//                 var bucket = _buckets[bucketIndex];
//                 for (; inBucketIndex < bucket.UserCount && rankCount + inBucketIndex <= endRank; inBucketIndex++)
//                 {
//                     result[resultIndex++] = new RankingListResponse
//                     {
//                         User = bucket.Users[inBucketIndex],
//                         Rank = rankCount + inBucketIndex + 1
//                     };
//                 }

//                 if (rankCount + inBucketIndex > endRank)
//                 {
//                     break;
//                 }

//                 rankCount += bucket.UserCount;
//                 inBucketIndex = 0;
//             }

//             return result;
//         }

//         public int GetRankingCount()
//         {
//             return _userCount;
//         }

//         /// <summary>
//         /// 每个桶
//         /// </summary>
//         internal class UserBucket
//         {
//             public IUser MinUser => Users[0];
//             public IUser MaxUser => Users[UserCount - 1];

//             public IUser[] Users { get; set; }
//             public int UserCount { get; set; }
//             public bool Full => UserCount >= Users.Length;
//             public bool Empty => UserCount == 0;

//             public UserBucket(IUser[] users, int userCount)
//             {
//                 Users = users;
//                 UserCount = userCount;
//             }

//             // public int IndexOf(int userId)
//             // {
//             //     for (int i = 0; i < UserCount; i++)
//             //     {
//             //         if (Users[i].Id == userId)
//             //         {
//             //             return i;
//             //         }
//             //     }

//             //     return -1;
//             // }

//             public int Insert(IUser user)
//             {
//                 int index = Array.BinarySearch(Users, 0, UserCount, user);
//                 if (index < 0)
//                 {
//                     index = ~index;
//                 }

//                 Array.Copy(Users, index, Users, index + 1, UserCount - index);
//                 Users[index] = user;
//                 UserCount++;
//                 return index;
//             }

//             public void Remove(IUser user)
//             {
//                 int index = Array.BinarySearch(Users, 0, UserCount, user);
//                 if (index < 0)
//                 {
//                     return;
//                 }

//                 Array.Copy(Users, index + 1, Users, index, UserCount - index - 1);
//                 Users[UserCount - 1] = null;
//                 UserCount--;
//             }

//             /// <summary>
//             /// 分裂成两个桶
//             /// </summary>
//             /// <param name="user"></param>
//             /// <param name="userIndex"></param>
//             /// <returns>右边的新桶</returns>
//             public UserBucket Split(IUser user, out int userIndex)
//             {
//                 int mid = UserCount / 2;
//                 userIndex = Array.BinarySearch(Users, 0, UserCount, user);
//                 if (userIndex < 0)
//                 {
//                     userIndex = ~userIndex;
//                 }

//                 IUser[] newUsers = new IUser[BucketSize];
//                 HashSet<int> newUserIds = new(BucketSize);
//                 int newUserCount = UserCount - mid;
//                 if (userIndex >= mid)
//                 {
//                     Array.Copy(Users, mid, newUsers, 0, userIndex - mid);
//                     newUsers[userIndex - mid] = user;
//                     Array.Copy(Users, userIndex, newUsers, userIndex - mid + 1, UserCount - userIndex);
//                     newUserCount++;
//                 }
//                 else
//                 {
//                     Array.Copy(Users, mid, newUsers, 0, UserCount - mid);
//                 }

//                 for (int i = 0; i < newUserCount; i++)
//                 {
//                     newUserIds.Add(newUsers[i].Id);
//                 }

//                 Array.Clear(Users, mid, UserCount - mid);

//                 UserCount = mid;
//                 UserBucket newBucket = new(newUsers, newUserCount);
//                 if (userIndex < mid)
//                     Insert(user);
//                 return newBucket;
//             }

//             public void Combine(UserBucket[] other)
//             {
//                 foreach (var bucket in other)
//                 {
//                     Array.Copy(bucket.Users, 0, Users, UserCount, bucket.UserCount);
//                     UserCount += bucket.UserCount;
//                 }
//             }
//         }

//         class TreeNode
//         {
//             public int Count { get; set; }
//             public IUser LeftUser { get; set; }
//             public IUser RightUser { get; set; }
//             public TreeNode Left { get; set; }
//             public TreeNode Right { get; set; }
//             public int BucketIndex { get; set; } = -1;
//         }
//     }
// }

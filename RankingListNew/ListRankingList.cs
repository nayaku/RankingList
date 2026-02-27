//namespace RankingListNew
//{
//    public class ListRankingList : IRankingList
//    {
//        private List<User> _users;

//        public ListRankingList(List<User> users)
//        {
//            _users = [.. users];
//            _users.Sort();
//        }

//        public int AddUser(User user)
//        {
//            _users.Add(user);
//            _users.Sort();
//            return _users.IndexOf(user);
//        }

//        public int UpdateUser(User user)
//        {
//            _users.Remove(user);
//            _users.Add(user);
//            _users.Sort();
//            return _users.IndexOf(user);
//        }

//        public int GetUserRank(int userId)
//        {
//            int index = _users.FindIndex(u => u.Id == userId);
//            return index;
//        }

//        public List<User> GetTopN(int topN)
//        {
//            int count = Math.Min(topN, _users.Count);
//            List<User> result = _users.GetRange(0, count);
//            return result;
//        }

//        public (List<User>, int) GetAroundUser(int userId, int aroundN)
//        {
//            int index = _users.FindIndex(u => u.Id == userId);
//            int start = Math.Max(0, index - aroundN);
//            int end = Math.Min(_users.Count - 1, index + aroundN);
//            int count = end - start + 1;
//            List<User> result = _users.GetRange(start, count);
//            return (result, index);
//        }

//        public int GetRankingCount()
//        {
//            return _users.Count;
//        }

//#if DEBUG
//        public void DebugPrint()
//        {
//        }
//#endif
//    }
//}
///*
//== Test t0_100 ===
//用户数: 0
//操作数: 83
//总耗时: 2 ms
//平均耗时: 24.10 ms/1000操作
//内存占用: 0.01 MB
//内存峰值: 0.03 MB
//测试日期: 2026/2/17 20:02:21
//            List<RankingListResponse> result = new(count);
//            for (int i = start; i <= end; i++)
//            {
//                result.Add(new RankingListResponse
//                {
//                    User = _users[i],
//                    Rank = i + 1
//                });
//            }
//            return result;
//        }

//        public int GetRankingCount()
//        {
//            return _users.Count;
//        }

//        public void DebugPrint()
//        {
//        }
//    }
//}
///*
//== Test t0_100 ===
//用户数: 0
//操作数: 83
//总耗时: 2 ms
//平均耗时: 24.10 ms/1000操作
//内存占用: 0.01 MB
//内存峰值: 0.03 MB
//测试日期: 2026/2/17 20:02:21
//== Test t0_100 End ===

//== Test t0_1w ===
//用户数: 0
//操作数: 9983
//总耗时: 159 ms
//平均耗时: 15.93 ms/1000操作
//内存占用: 4.03 MB
//内存峰值: 4.51 MB
//测试日期: 2026/2/17 20:02:21
//== Test t0_1w End ===

//== Test t100_1w ===
//用户数: 100
//操作数: 10000
//总耗时: 173 ms
//平均耗时: 17.30 ms/1000操作
//内存占用: 4.09 MB
//内存峰值: 4.57 MB
//测试日期: 2026/2/17 20:02:22
//== Test t100_1w End ===

//== Test t1k_10w ===
//用户数: 1000
//操作数: 100000
//总耗时: 2898 ms
//平均耗时: 28.98 ms/1000操作
//内存占用: 40.76 MB
//内存峰值: 41.61 MB
//测试日期: 2026/2/17 20:02:25
//== Test t1k_10w End ===
//*/
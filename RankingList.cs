namespace RankingList
{
    public class RankingList
    {
        public List<User> Users { get; set; } = [];

        public RankingList()
        {
        }

        public RankingList(List<User> users)
        {
            Users = users;
            Users.Sort();
        }

        /// <summary>
        /// 获取用户排名
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public RankingListSingleResponse GetUserRanking(int userId)
        {
            for (int i = 0; i < Users.Count; i++)
            {
                if (Users[i].ID == userId)
                {
                    return new RankingListSingleResponse
                    {
                        User = Users[i],
                        Rank = i + 1
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// 获取前N名用户排名
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private List<RankingListSingleResponse> GetTopNRankings(int n)
        {
            List<RankingListSingleResponse> topRankings = [];
            for (int i = 0; i < n && i < Users.Count; i++)
            {
                topRankings.Add(new RankingListSingleResponse
                {
                    User = Users[i],
                    Rank = i + 1
                });
            }

            return topRankings;
        }

        /// <summary>
        /// 获取总用户数
        /// </summary>
        /// <returns></returns>
        public int GetTotalUsers()
        {
            return Users.Count;
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="user"></param>
        public void AddUser(User user)
        {
            // Insert user in sorted order
            int index = Users.BinarySearch(user);
            if (index < 0)
            {
                index = ~index; // Get the insertion point
            }

            Users.Insert(index, user);
        }

        /// <summary>
        /// 更新用户
        /// </summary>
        /// <param name="user"></param>
        public void UpdateUser(User user)
        {
            // Remove old user and re-insert to maintain order
            Users.RemoveAll(u => u.ID == user.ID);
            AddUser(user);
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="user"></param>
        public void DeleteUser(User user)
        {
            Users.RemoveAll(u => u.ID == user.ID);
        }

        /// <summary>
        /// 获取用户周围的排名
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public List<RankingListSingleResponse> GetUsersRankingAroundUser(int userId, int range)
        {
            List<RankingListSingleResponse> surroundingRankings = [];
            int userIndex = Users.FindIndex(u => u.ID == userId);
            if (userIndex == -1) return surroundingRankings;
            int start = Math.Max(0, userIndex - range);
            int end = Math.Min(Users.Count - 1, userIndex + range);
            for (int i = start; i <= end; i++)
            {
                surroundingRankings.Add(new RankingListSingleResponse
                {
                    User = Users[i],
                    Rank = i + 1
                });
            }

            return surroundingRankings;
        }

        public RankingListMutiResponse GetRankingListMutiResponse(int userId, int topN, int range)
        {
            return new RankingListMutiResponse
            {
                TopNUsers = GetTopNRankings(topN),
                RankingAroundUsers = GetUsersRankingAroundUser(userId, range),
                TotalUsers = GetTotalUsers()
            };
        }
    }
}

namespace RankingList
{
    internal class SimpleRankingList : IRankingList
    {
        public List<IUser> Users { get; set; }

        public SimpleRankingList(IUser[] users) 
        {
            Users = [.. users];
            Users.Sort();
        }
      
        public RankingListResponse AddUser(IUser user)
        {
            Users.Add(user);
            Users.Sort();
            return new RankingListResponse
            {
                User = user,
                Rank = Users.IndexOf(user) + 1
            };
        }

        public RankingListResponse UpdateUser(IUser user)
        {
            var existingUser = Users.FirstOrDefault(u => u.Id == user.Id);
            if (existingUser == null)
            {
                throw new ArgumentException($"用户 {user.Id} 不存在");
            }
            Users.Remove(existingUser);
            Users.Add(user);
            Users.Sort();
            return new RankingListResponse
            {
                User = user,
                Rank = Users.IndexOf(user) + 1
            };
        }

        RankingListResponse IRankingList.GetUserRank(int userId)
        {
            var index = Users.FindIndex(u => u.Id == userId);
            if (index == -1) return null;
            return new RankingListResponse
            {
                User = Users[index],
                Rank = index + 1
            };
        }

        public RankingListResponse[] GetTopN(int topN)
        {
            var count = Math.Min(topN, Users.Count);
            var result = new RankingListResponse[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new RankingListResponse
                {
                    User = Users[i],
                    Rank = i + 1
                };
            }
            return result;
        }

        public RankingListResponse[] GetAroundUser(int userId, int aroundN)
        {
            var index = Users.FindIndex(u => u.Id == userId);
            if (index == -1) return [];
            int start = Math.Max(0, index - aroundN);
            int end = Math.Min(Users.Count - 1, index + aroundN);
            int count = end - start + 1;
            var result = new RankingListResponse[count];
            for (int i = start; i <= end; i++)
            {
                result[i - start] = new RankingListResponse
                {
                    User = Users[i],
                    Rank = i + 1
                };
            }
            return result;
        }

        public int GetRankingCount()
        {
            return Users.Count;
        }
    }
}
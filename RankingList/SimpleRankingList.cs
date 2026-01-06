namespace RankingList
{
    internal class SimpleRankingList : IRankingList
    {
        public List<User> Users { get; set; }
        private readonly object _lock = new();

        public SimpleRankingList(List<User> users)
        {
            Users = users;
            Users.Sort();
        }
        public void AddOrUpdateUser(User user)
        {
            lock (_lock)
            {
                var existingUser = Users.FirstOrDefault(u => u.ID == user.ID);
                if (existingUser != null)
                {
                    Users.Remove(existingUser);
                }
                Users.Add(user);
                Users.Sort();
            }
        }
        public void AddOrUpdateUser(int userId, int score, DateTime lastActive)
        {
            var user = new User
            {
                ID = userId,
                Score = score,
                LastActive = lastActive
            };
            AddOrUpdateUser(user);
        }
        public void RemoveUser(int userId)
        {
            lock (_lock)
            {
                var user = Users.FirstOrDefault(u => u.ID == userId);
                if (user != null)
                {
                    Users.Remove(user);
                }
            }
        }
        public RankingListSingleResponse GetUserRank(int userId)
        {
            lock (_lock)
            {
                var index = Users.FindIndex(u => u.ID == userId);
                if (index == -1) return null;
                return new RankingListSingleResponse
                {
                    User = Users[index],
                    Rank = index + 1
                };
            }
        }
        public RankingListMutiResponse GetRankingListMutiResponse(int topN, int aroundUserId, int aroundN)
        {
            lock (_lock)
            {
                var response = new RankingListMutiResponse
                {
                    TopNUsers = [],
                    RankingAroundUsers = [],
                    TotalUsers = Users.Count
                };
                // Top N users
                for (int i = 0; i < Math.Min(topN, Users.Count); i++)
                {
                    response.TopNUsers.Add(new RankingListSingleResponse
                    {
                        User = Users[i],
                        Rank = i + 1
                    });
                }
                // Users around a specific user
                var index = Users.FindIndex(u => u.ID == aroundUserId);
                if (index != -1)
                {
                    int start = Math.Max(0, index - aroundN);
                    int end = Math.Min(Users.Count - 1, index + aroundN);
                    for (int i = start; i <= end; i++)
                    {
                        response.RankingAroundUsers.Add(new RankingListSingleResponse
                        {
                            User = Users[i],
                            Rank = i + 1
                        });
                    }
                }
                return response;
            }
        }
    }
}

namespace RankingListNew
{
    public class ListRankingList : IRankingList
    {
        private List<User> _users;

        public ListRankingList()
        {
            _users = [];
        }

        public ListRankingList(List<User> users)
        {
            _users = [.. users];
            _users.Sort();
        }

        public RankingListResponse AddUser(User user)
        {
            _users.Add(user);
            _users.Sort();
            return new RankingListResponse
            {
                User = user,
                Rank = _users.IndexOf(user) + 1
            };
        }

        public RankingListResponse UpdateUser(User user)
        {
            _users.Remove(user);
            _users.Add(user);
            _users.Sort();
            return new RankingListResponse
            {
                User = user,
                Rank = _users.IndexOf(user) + 1
            };
        }

        RankingListResponse IRankingList.GetUserRank(int userId)
        {
            var index = _users.FindIndex(u => u.Id == userId);
            return new RankingListResponse
            {
                User = _users[index],
                Rank = index + 1
            };
        }

        public List<RankingListResponse> GetTopN(int topN)
        {
            var count = Math.Min(topN, _users.Count);
            var result = new List<RankingListResponse>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(new RankingListResponse
                {
                    User = _users[i],
                    Rank = i + 1
                });
            }
            return result;
        }

        public List<RankingListResponse> GetAroundUser(int userId, int aroundN)
        {
            var index = _users.FindIndex(u => u.Id == userId);
            int start = Math.Max(0, index - aroundN);
            int end = Math.Min(_users.Count - 1, index + aroundN);
            int count = end - start + 1;
            var result = new List<RankingListResponse>(count);
            for (int i = start; i <= end; i++)
            {
                result.Add(new RankingListResponse
                {
                    User = _users[i],
                    Rank = i + 1
                });
            }
            return result;
        }

        public int GetRankingCount()
        {
            return _users.Count;
        }

        public void DebugPrint()
        {
        }
    }
}

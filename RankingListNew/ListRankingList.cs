namespace RankingListNew
{
    public class ListRankingList : IRankingList
    {
        private List<User> _users;

        public ListRankingList(List<User> users)
        {
            _users = [.. users];
            _users.Sort();
        }

        public int AddUser(User user)
        {
            _users.Add(user);
            _users.Sort();
            return _users.IndexOf(user);
        }

        public int UpdateUser(User user)
        {
            _users.Remove(user);
            _users.Add(user);
            _users.Sort();
            return _users.IndexOf(user);
        }

        public int GetUserRank(int userId)
        {
            int index = _users.FindIndex(u => u.Id == userId);
            return index;
        }

        public User[] GetTopN(int topN)
        {
            int count = Math.Min(topN, _users.Count);
            User[] result = [.. _users.GetRange(0, count)];
            return result;
        }

        public (User[], int) GetAroundUser(int userId, int aroundN)
        {
            int index = _users.FindIndex(u => u.Id == userId);
            int start = Math.Max(0, index - aroundN);
            int end = Math.Min(_users.Count - 1, index + aroundN);
            int count = end - start + 1;
            User[] result = [.. _users.GetRange(start, count)];
            return (result, index);
        }

        public int GetRankingCount()
        {
            return _users.Count;
        }

#if DEBUG
        public void DebugPrint()
        {
        }
#endif
    }
}

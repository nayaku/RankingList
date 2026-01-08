namespace RankingList
{
    public class User : IUser
    {
        public int Id { get; set; }
        public int Score { get; set; }
        public DateTime LastActive { get; set; }
        public int CompareTo(IUser? other)
        {
            if (other is not User otherUser) return 1;
            if (Score == otherUser.Score)
                return -LastActive.CompareTo(otherUser.LastActive);
            return -Score.CompareTo(otherUser.Score);
        }
    }
}

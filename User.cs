namespace RankingList
{
    public class User : IComparable<User>
    {
        public int ID { get; set; }
        public int Score { get; set; }
        public DateTime LastActive { get; set; }
        public int CompareTo(User other)
        {
            if (other == null) return 1;
            if (Score == other.Score)
                return LastActive.CompareTo(other.LastActive);
            return Score.CompareTo(other.Score);
        }
    }
}

using System.Text.Json.Serialization;

namespace RankingListNew
{
    public readonly struct User : IComparable<User>
    {
        public readonly int Id;
        public readonly int Score;
        public readonly DateTime LastUpdateTime;

        [JsonConstructor]
        public User(int id = -1, int score = 0, DateTime lastUpdateTime = default)
        {
            Id = id;
            Score = score;
            LastUpdateTime = lastUpdateTime;
        }

        public int CompareTo(User other)
        {
            if (Score != other.Score)
                return -Score.CompareTo(other.Score);
            else if (LastUpdateTime != other.LastUpdateTime)
                return LastUpdateTime.CompareTo(other.LastUpdateTime);
            else
                return Id.CompareTo(other.Id);
        }

        public bool Equals(User other)
        {
            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;
            if (obj is User other)
                return Equals(other);
            return false;
        }
    }
}

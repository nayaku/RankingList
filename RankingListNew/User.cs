using System.Text.Json.Serialization;

namespace RankingListNew
{
    public readonly struct User : IComparable<User>
    {
        public readonly int Id;
        public readonly int Score;
        public readonly DateTime LastActive;

        [JsonConstructor]
        public User(int id = -1, int score = 0, DateTime lastActive = default)
        {
            Id = id;
            Score = score;
            LastActive = lastActive;
        }

        public int CompareTo(User other)
        {
            if (Score != other.Score)
                return -Score.CompareTo(other.Score);
            else if (LastActive != other.LastActive)
                return LastActive.CompareTo(other.LastActive);
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

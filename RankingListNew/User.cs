namespace RankingListNew
{
    public readonly struct User : IComparable<User>, IEquatable<User>
    {
        public readonly int Id;
        public readonly int Score;
        public readonly DateTime LastActive;

        public User(int id, int score, DateTime lastActive)
        {
            Id = id;
            Score = score;
            LastActive = lastActive;
        }

        public int CompareTo(User other)
        {
            if (Score == other.Score)
                return -LastActive.CompareTo(other.LastActive);
            else if (LastActive == other.LastActive)
                return -Id.CompareTo(other.Id);
            return -Score.CompareTo(other.Score);
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

        public static bool operator ==(User left, User right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(User left, User right)
        {
            return !(left == right);
        }

        public static bool operator <(User left, User right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(User left, User right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(User left, User right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(User left, User right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}

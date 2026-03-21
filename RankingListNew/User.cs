using System.Text.Json.Serialization;

namespace RankingListNew
{
    [Serializable]
    public readonly struct User : IComparable<User>
    {
        /// <summary>
        /// 玩家的唯一标识符
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// 玩家的分数
        /// </summary>
        public readonly int Score;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public readonly DateTime LastUpdateTime;

        [JsonConstructor]
        public User(int id = -1, int score = 0, DateTime lastUpdateTime = default)
        {
            Id = id;
            Score = score;
            LastUpdateTime = lastUpdateTime;
        }

        /// <summary>
        /// 比较方法，实现 IComparable 接口
        /// 排序规则：分数降序 → 更新时间升序 → ID升序
        /// </summary>
        public int CompareTo(User other)
        {
            int compareResult = -Score.CompareTo(other.Score);
            if (compareResult != 0) 
                return compareResult;
            compareResult = LastUpdateTime.CompareTo(other.LastUpdateTime);
            if (compareResult != 0) 
                return compareResult;
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

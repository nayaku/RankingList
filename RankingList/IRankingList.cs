using System.Text.Json.Serialization;

namespace RankingList
{
    [JsonDerivedType(typeof(User), "user")]
    public interface IUser : IComparable<IUser>
    {
        int Id { get; set; }
    }
    public class RankingListResponse
    {
        public IUser? User { get; set; }
        public int Rank { get; set; }
    }
    public interface IRankingList
    {
        RankingListResponse AddUser(IUser user);
        RankingListResponse UpdateUser(IUser user);
        RankingListResponse GetUserRank(int userId);
        RankingListResponse[] GetTopN(int topN);
        RankingListResponse[] GetAroundUser(int userId, int aroundN);
        int GetRankingCount();
    }
}

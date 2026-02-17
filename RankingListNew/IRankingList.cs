namespace RankingListNew
{
    public interface IRankingList
    {
        RankingListResponse AddUser(User user);
        RankingListResponse UpdateUser(User user);
        RankingListResponse GetUserRank(int userId);
        List<RankingListResponse> GetTopN(int topN);
        List<RankingListResponse> GetAroundUser(int userId, int aroundN);
        int GetRankingCount();
        void DebugPrint(); /* 仅用于调试，输出排行榜当前状态 */
    }
    public record struct RankingListResponse
    {
        public User User;
        public int Rank;
    }
}

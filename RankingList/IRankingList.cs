namespace RankingList
{
    public interface IRankingList
    {
        void AddOrUpdateUser(int userId, int score, DateTime lastActive);
        RankingListSingleResponse GetUserRank(int userId);
        RankingListMutiResponse GetRankingListMutiResponse(int topN, int aroundUserId, int aroundN);
    }
}

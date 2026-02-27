namespace RankingListNew
{
    public interface IRankingList
    {
        int AddUser(User user);
        int UpdateUser(User user);
        int GetUserRank(int userId);
        User[] GetTopN(int topN);
        (User[], int) GetAroundUser(int userId, int aroundN);
        int GetRankingCount();
#if DEBUG
        void DebugPrint(); /* 仅用于调试，输出排行榜当前状态 */
#endif
    }
}

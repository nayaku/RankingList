namespace RankingListNew
{
    public interface IRankingList
    {
        /// <summary>
        /// 添加玩家到排行榜
        /// </summary>
        /// <param name="user">要添加的玩家</param>
        /// <returns>玩家的排名（从0开始）</returns>
        int AddUser(User user);

        /// <summary>
        /// 更新玩家分数（先删除旧数据，再插入新数据）
        /// </summary>
        /// <param name="user">包含新分数的玩家信息</param>
        /// <returns>玩家的新排名</returns>
        int UpdateUser(User user);

        /// <summary>
        /// 获取玩家的当前排名
        /// </summary>
        /// <param name="userId">玩家ID</param>
        /// <returns>玩家排名（从0开始）</returns>
        int GetUserRank(int userId);

        /// <summary>
        /// 获取排行榜前N名玩家
        /// </summary>
        /// <param name="topN">要获取的玩家数量</param>
        /// <returns>按排名排序的玩家数组</returns>
        User[] GetTopN(int topN);

        /// <summary>
        /// 获取目标玩家周围的排名
        /// </summary>
        /// <param name="userId">目标玩家ID</param>
        /// <param name="aroundN">左右各获取的玩家数量</param>
        /// <returns>玩家数组和目标玩家的排名</returns>
        (User[], int) GetAroundUser(int userId, int aroundN);

        /// <summary>
        /// 获取排行榜中的玩家总数
        /// </summary>
        /// <returns>玩家数量</returns>
        int GetRankingCount();
#if DEBUG
        void DebugPrint(); /* 仅用于调试，输出排行榜当前状态 */
#endif
    }
}

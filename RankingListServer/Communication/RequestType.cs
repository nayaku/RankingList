namespace RankingListServer.Communication
{
    /// <summary>
    /// 排行榜操作请求类型枚举
    /// </summary>
    public enum RequestType
    {
        /// <summary>
        /// 初始化请求
        /// </summary>
        Initialize,

        /// <summary>
        /// 添加或更新用户
        /// </summary>
        AddOrUpdateUser,
        
        /// <summary>
        /// 获取用户排名
        /// </summary>
        GetUserRank,
        
        /// <summary>
        /// 获取排行榜多响应
        /// </summary>
        GetRankingListMutiResponse,
        
        /// <summary>
        /// 获取当前内存使用情况
        /// </summary>
        GetMemoryUsage
    }
}
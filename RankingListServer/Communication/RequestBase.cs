namespace RankingListServer.Communication
{
    /// <summary>
    /// 请求消息基类
    /// </summary>
    public abstract class RequestBase
    {
        /// <summary>
        /// 请求类型
        /// </summary>
        public RequestType Type { get; set; }
        
        /// <summary>
        /// 请求ID，用于匹配请求和响应
        /// </summary>
        public Guid RequestId { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="type">请求类型</param>
        protected RequestBase(RequestType type)
        {
            Type = type;
            RequestId = Guid.NewGuid();
        }
    }
    
    /// <summary>
    /// 添加或更新用户请求
    /// </summary>
    public class AddOrUpdateUserRequest : RequestBase
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// 分数
        /// </summary>
        public int Score { get; set; }
        
        /// <summary>
        /// 最后活跃时间
        /// </summary>
        public DateTime LastActive { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public AddOrUpdateUserRequest() : base(RequestType.AddOrUpdateUser)
        {}
    }
    
    /// <summary>
    /// 获取用户排名请求
    /// </summary>
    public class GetUserRankRequest : RequestBase
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public GetUserRankRequest() : base(RequestType.GetUserRank)
        {}
    }
    
    /// <summary>
    /// 获取排行榜多响应请求
    /// </summary>
    public class GetRankingListMutiResponseRequest : RequestBase
    {
        /// <summary>
        /// 前N名用户数量
        /// </summary>
        public int TopN { get; set; }
        
        /// <summary>
        /// 周围用户的中心用户ID
        /// </summary>
        public int AroundUserId { get; set; }
        
        /// <summary>
        /// 周围用户数量
        /// </summary>
        public int AroundN { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public GetRankingListMutiResponseRequest() : base(RequestType.GetRankingListMutiResponse)
        {}
    }
    
    /// <summary>
    /// 获取内存使用情况请求
    /// </summary>
    public class GetMemoryUsageRequest : RequestBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public GetMemoryUsageRequest() : base(RequestType.GetMemoryUsage)
        {}
    }
}
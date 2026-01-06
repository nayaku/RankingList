using RankingList;

namespace RankingListServer.Communication
{
    /// <summary>
    /// 响应消息基类
    /// </summary>
    public abstract class ResponseBase
    {
        /// <summary>
        /// 对应的请求ID
        /// </summary>
        public Guid RequestId { get; set; }
        
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="requestId">对应的请求ID</param>
        protected ResponseBase(Guid requestId)
        {
            RequestId = requestId;
            Success = true;
        }
    }
    
    /// <summary>
    /// 添加或更新用户响应
    /// </summary>
    public class AddOrUpdateUserResponse : ResponseBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="requestId">对应的请求ID</param>
        public AddOrUpdateUserResponse(Guid requestId) : base(requestId)
        {}
    }
    
    /// <summary>
    /// 获取用户排名响应
    /// </summary>
    public class GetUserRankResponse : ResponseBase
    {
        /// <summary>
        /// 用户排名结果
        /// </summary>
        public RankingListSingleResponse? Result { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="requestId">对应的请求ID</param>
        public GetUserRankResponse(Guid requestId) : base(requestId)
        {}
    }
    
    /// <summary>
    /// 获取排行榜多响应响应
    /// </summary>
    public class GetRankingListMutiResponseResponse : ResponseBase
    {
        /// <summary>
        /// 排行榜多响应结果
        /// </summary>
        public RankingListMutiResponse? Result { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="requestId">对应的请求ID</param>
        public GetRankingListMutiResponseResponse(Guid requestId) : base(requestId)
        {}
    }
    
    /// <summary>
    /// 获取内存使用情况响应
    /// </summary>
    public class GetMemoryUsageResponse : ResponseBase
    {
        /// <summary>
        /// 当前内存使用量（字节）
        /// </summary>
        public long CurrentMemoryUsage { get; set; }
        
        /// <summary>
        /// 峰值内存使用量（字节）
        /// </summary>
        public long PeakMemoryUsage { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="requestId">对应的请求ID</param>
        public GetMemoryUsageResponse(Guid requestId) : base(requestId)
        {}
    }
}
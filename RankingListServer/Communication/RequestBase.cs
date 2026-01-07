using RankingList;
using System.IO;

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

        #region Binary Serialization

        /// <summary>
        /// Serialize this request to binary
        /// </summary>
        public virtual byte[] Serialize()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // Serialize request type and ID
                BinarySerializer.SerializeInt(writer, (int)Type);
                BinarySerializer.SerializeGuid(writer, RequestId);

                // Serialize request-specific data
                SerializeData(writer);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserialize a request from binary
        /// </summary>
        public static RequestBase? Deserialize(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // Deserialize request type
                int typeValue = BinarySerializer.DeserializeInt(reader);
                RequestType type = (RequestType)typeValue;

                // Deserialize request ID
                Guid requestId = BinarySerializer.DeserializeGuid(reader);

                // Create and deserialize the appropriate request type
                RequestBase? request = CreateRequest(type, requestId);
                if (request != null)
                {
                    request.DeserializeData(reader);
                }

                return request;
            }
        }

        /// <summary>
        /// Serialize request-specific data
        /// </summary>
        protected abstract void SerializeData(BinaryWriter writer);

        /// <summary>
        /// Deserialize request-specific data
        /// </summary>
        protected abstract void DeserializeData(BinaryReader reader);

        /// <summary>
        /// Create a request object based on type
        /// </summary>
        private static RequestBase? CreateRequest(RequestType type, Guid requestId)
        {
            switch (type)
            {
                case RequestType.Initialize:
                    return new InitializeRequest { RequestId = requestId };
                case RequestType.AddOrUpdateUser:
                    return new AddOrUpdateUserRequest { RequestId = requestId };
                case RequestType.GetUserRank:
                    return new GetUserRankRequest { RequestId = requestId };
                case RequestType.GetRankingListMutiResponse:
                    return new GetRankingListMutiResponseRequest { RequestId = requestId };
                case RequestType.GetMemoryUsage:
                    return new GetMemoryUsageRequest { RequestId = requestId };
                default:
                    return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// 初始化请求
    /// </summary>
    public class InitializeRequest : RequestBase
    {
        public User[] Users { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public InitializeRequest() : base(RequestType.Initialize)
        {
            Users = Array.Empty<User>();
        }

        protected override void SerializeData(BinaryWriter writer)
        {
            BinarySerializer.SerializeUserArray(writer, Users);
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            Users = BinarySerializer.DeserializeUserArray(reader);
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

        protected override void SerializeData(BinaryWriter writer)
        {
            BinarySerializer.SerializeInt(writer, UserId);
            BinarySerializer.SerializeInt(writer, Score);
            BinarySerializer.SerializeDateTime(writer, LastActive);
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            UserId = BinarySerializer.DeserializeInt(reader);
            Score = BinarySerializer.DeserializeInt(reader);
            LastActive = BinarySerializer.DeserializeDateTime(reader);
        }
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

        protected override void SerializeData(BinaryWriter writer)
        {
            BinarySerializer.SerializeInt(writer, UserId);
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            UserId = BinarySerializer.DeserializeInt(reader);
        }
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

        protected override void SerializeData(BinaryWriter writer)
        {
            BinarySerializer.SerializeInt(writer, TopN);
            BinarySerializer.SerializeInt(writer, AroundUserId);
            BinarySerializer.SerializeInt(writer, AroundN);
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            TopN = BinarySerializer.DeserializeInt(reader);
            AroundUserId = BinarySerializer.DeserializeInt(reader);
            AroundN = BinarySerializer.DeserializeInt(reader);
        }
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

        protected override void SerializeData(BinaryWriter writer)
        {
            // No additional data to serialize
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            // No additional data to deserialize
        }
    }
}
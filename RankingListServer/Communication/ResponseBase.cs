using RankingList;
using System.IO;

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

        #region Binary Serialization

        /// <summary>
        /// Serialize this response to binary
        /// </summary>
        public virtual byte[] Serialize()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // Serialize common response fields
                BinarySerializer.SerializeGuid(writer, RequestId);
                BinarySerializer.SerializeBool(writer, Success);
                BinarySerializer.SerializeString(writer, ErrorMessage);

                // Serialize response-specific data if successful
                if (Success)
                {
                    SerializeData(writer);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserialize a response from binary
        /// </summary>
        public static ResponseBase? Deserialize(byte[] data, RequestType requestType)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // Deserialize common response fields
                Guid requestId = BinarySerializer.DeserializeGuid(reader);
                bool success = BinarySerializer.DeserializeBool(reader);
                string? errorMessage = BinarySerializer.DeserializeString(reader);

                // Create the appropriate response type based on the request type
                ResponseBase? response = CreateResponse(requestType, requestId);
                if (response != null)
                {
                    response.Success = success;
                    response.ErrorMessage = errorMessage;

                    // Deserialize response-specific data if successful
                    if (success)
                    {
                        response.DeserializeData(reader);
                    }
                }

                return response;
            }
        }

        /// <summary>
        /// Serialize response-specific data
        /// </summary>
        protected abstract void SerializeData(BinaryWriter writer);

        /// <summary>
        /// Deserialize response-specific data
        /// </summary>
        protected abstract void DeserializeData(BinaryReader reader);

        /// <summary>
        /// Create a response object based on request type
        /// </summary>
        private static ResponseBase? CreateResponse(RequestType requestType, Guid requestId)
        {
            switch (requestType)
            {
                case RequestType.Initialize:
                    return new InitializeResponse(requestId);
                case RequestType.AddOrUpdateUser:
                    return new AddOrUpdateUserResponse(requestId);
                case RequestType.GetUserRank:
                    return new GetUserRankResponse(requestId);
                case RequestType.GetRankingListMutiResponse:
                    return new GetRankingListMutiResponseResponse(requestId);
                case RequestType.GetMemoryUsage:
                    return new GetMemoryUsageResponse(requestId);
                default:
                    return null;
            }
        }

        #endregion
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

        protected override void SerializeData(BinaryWriter writer)
        {
            // No additional data to serialize
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            // No additional data to deserialize
        }
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

        protected override void SerializeData(BinaryWriter writer)
        {
            if (Result == null)
            {
                // Write null indicator
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                BinarySerializer.SerializeRankingListSingleResponse(writer, Result);
            }
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            bool hasResult = reader.ReadBoolean();
            if (hasResult)
            {
                Result = BinarySerializer.DeserializeRankingListSingleResponse(reader);
            }
            else
            {
                Result = null;
            }
        }
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

        protected override void SerializeData(BinaryWriter writer)
        {
            if (Result == null)
            {
                // Write null indicator
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                BinarySerializer.SerializeRankingListMutiResponse(writer, Result);
            }
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            bool hasResult = reader.ReadBoolean();
            if (hasResult)
            {
                Result = BinarySerializer.DeserializeRankingListMutiResponse(reader);
            }
            else
            {
                Result = null;
            }
        }
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

        protected override void SerializeData(BinaryWriter writer)
        {
            BinarySerializer.SerializeLong(writer, CurrentMemoryUsage);
            BinarySerializer.SerializeLong(writer, PeakMemoryUsage);
        }

        protected override void DeserializeData(BinaryReader reader)
        {
            CurrentMemoryUsage = BinarySerializer.DeserializeLong(reader);
            PeakMemoryUsage = BinarySerializer.DeserializeLong(reader);
        }
    }

    /// <summary>
    /// 初始化响应
    /// </summary>
    public class InitializeResponse : ResponseBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="requestId">对应的请求ID</param>
        public InitializeResponse(Guid requestId) : base(requestId)
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
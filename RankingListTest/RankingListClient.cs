using RankingList;
using RankingListServer.Communication;
using System.IO.Pipes;
using System.Text.Json;

namespace RankingListTest
{
    /// <summary>
    /// 排行榜客户端，通过命名管道与服务器通信
    /// </summary>
    public class RankingListClient : IRankingList, IDisposable
    {
        private readonly string _pipeName;
        private NamedPipeClientStream? _pipeStream;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private Dictionary<Guid, TaskCompletionSource<ResponseBase>> _pendingRequests = new Dictionary<Guid, TaskCompletionSource<ResponseBase>>();
        private readonly Thread _readThread;
        private bool _isRunning = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pipeName">命名管道名称</param>
        public RankingListClient(string pipeName = "RankingListPipe")
        {
            _pipeName = pipeName;
            ConnectToServer();
            _readThread = new Thread(ReadResponses);
            _readThread.Start();
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        private void ConnectToServer()
        {
            try
            {
                Console.WriteLine($"Attempting to connect to pipe: \\.\\pipe\\{_pipeName}");
                _pipeStream = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous,
                    System.Security.Principal.TokenImpersonationLevel.Anonymous);

                Console.WriteLine("Connecting to server with 10 seconds timeout...");
                _pipeStream.Connect(10000); // Timeout after 10 seconds
                Console.WriteLine("Connection established, setting read mode...");
                _pipeStream.ReadMode = PipeTransmissionMode.Message;
                Console.WriteLine("Connected to RankingList server successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to server: {ex.Message}");
                Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 读取服务器响应的线程
        /// </summary>
        private void ReadResponses()
        {
            while (_isRunning && _pipeStream != null && _pipeStream.IsConnected)
            {
                try
                {
                    // Read message length
                    byte[] lengthBuffer = new byte[4];
                    int bytesRead = _pipeStream.Read(lengthBuffer, 0, 4);
                    if (bytesRead != 4)
                    {
                        Console.WriteLine("Failed to read message length from server.");
                        break;
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (messageLength <= 0)
                    {
                        Console.WriteLine("Invalid message length from server.");
                        continue;
                    }

                    // Read message content
                    byte[] messageBuffer = new byte[messageLength];
                    bytesRead = 0;
                    while (bytesRead < messageLength)
                    {
                        int read = _pipeStream.Read(messageBuffer, bytesRead, messageLength - bytesRead);
                        if (read == 0)
                        {
                            Console.WriteLine("Server disconnected.");
                            break;
                        }
                        bytesRead += read;
                    }

                    if (bytesRead != messageLength)
                    {
                        Console.WriteLine("Failed to read complete message from server.");
                        break;
                    }

                    // Deserialize response - use a simpler approach for .NET 8 compatibility
                    string json = System.Text.Encoding.UTF8.GetString(messageBuffer);
                    ResponseBase? response = null;
                    
                    // First determine the response type by checking the result property
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;
                    
                    // Check for properties that identify the response type
                    if (root.TryGetProperty("Result", out JsonElement resultElement))
                    {
                        // Check if Result has User property (GetUserRankResponse)
                        if (resultElement.ValueKind == JsonValueKind.Object && 
                            resultElement.TryGetProperty("User", out _))
                        {
                            response = JsonSerializer.Deserialize<GetUserRankResponse>(json);
                        }
                        // Check if Result has TopNUsers property (GetRankingListMutiResponseResponse)
                        else if (resultElement.ValueKind == JsonValueKind.Object && 
                                 resultElement.TryGetProperty("TopNUsers", out _))
                        {
                            response = JsonSerializer.Deserialize<GetRankingListMutiResponseResponse>(json);
                        }
                    }
                    // If no Result property, check for CurrentMemoryUsage (GetMemoryUsageResponse)
                    else if (root.TryGetProperty("CurrentMemoryUsage", out _))
                    {
                        response = JsonSerializer.Deserialize<GetMemoryUsageResponse>(json);
                    }
                    // Otherwise, it's likely AddOrUpdateUserResponse
                    else
                    {
                        response = JsonSerializer.Deserialize<AddOrUpdateUserResponse>(json);
                    }

                    if (response != null)
                    {
                        // Complete the pending task
                        lock (_lock)
                        {
                            if (_pendingRequests.TryGetValue(response.RequestId, out var tcs))
                            {
                                _pendingRequests.Remove(response.RequestId);
                                tcs.SetResult(response);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine($"Error reading response: {ex.Message}");
                        Thread.Sleep(1000); // Wait before trying to reconnect
                        try
                        {
                            ConnectToServer();
                        }
                        catch { /* Ignore reconnection errors */ }
                    }
                }
            }
        }

        /// <summary>
        /// 发送请求到服务器
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="request">请求对象</param>
        /// <returns>响应对象</returns>
        private async Task<TResponse> SendRequestAsync<TResponse>(RequestBase request) where TResponse : ResponseBase
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RankingListClient));

            if (_pipeStream == null || !_pipeStream.IsConnected)
                ConnectToServer();

            var tcs = new TaskCompletionSource<ResponseBase>();
            lock (_lock)
            {
                _pendingRequests[request.RequestId] = tcs;
            }

            try
            {
                // Serialize request - use concrete type for compatibility with .NET 8
                string json;
                if (request is AddOrUpdateUserRequest addUpdateRequest)
                {
                    json = JsonSerializer.Serialize(addUpdateRequest);
                }
                else if (request is GetUserRankRequest userRankRequest)
                {
                    json = JsonSerializer.Serialize(userRankRequest);
                }
                else if (request is GetRankingListMutiResponseRequest rankingListRequest)
                {
                    json = JsonSerializer.Serialize(rankingListRequest);
                }
                else if (request is GetMemoryUsageRequest memoryRequest)
                {
                    json = JsonSerializer.Serialize(memoryRequest);
                }
                else
                {
                    throw new NotSupportedException($"Request type {request.GetType().Name} is not supported.");
                }

                // Convert to bytes
                byte[] messageBuffer = System.Text.Encoding.UTF8.GetBytes(json);
                int messageLength = messageBuffer.Length;

                // Write message length and content
                byte[] lengthBuffer = BitConverter.GetBytes(messageLength);
                lock (_lock)
                {
                    _pipeStream!.Write(lengthBuffer, 0, 4);
                    _pipeStream.Write(messageBuffer, 0, messageLength);
                    _pipeStream.Flush();
                    Console.WriteLine($"Sent request: {request.Type}, Length: {messageLength}");
                }

                // Wait for response
                var response = await tcs.Task;
                if (!response.Success)
                {
                    throw new Exception(response.ErrorMessage);
                }

                return (TResponse)response;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _pendingRequests.Remove(request.RequestId);
                }
                throw;
            }
        }

        /// <summary>
        /// 添加或更新用户
        /// </summary>
        /// <param name="user">用户对象</param>
        public void AddOrUpdateUser(User user)
        {
            AddOrUpdateUser(user.ID, user.Score, user.LastActive);
        }

        /// <summary>
        /// 添加或更新用户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="score">分数</param>
        /// <param name="lastActive">最后活跃时间</param>
        public void AddOrUpdateUser(int userId, int score, DateTime lastActive)
        {
            var request = new AddOrUpdateUserRequest
            {
                UserId = userId,
                Score = score,
                LastActive = lastActive
            };

            var response = SendRequestAsync<AddOrUpdateUserResponse>(request).Result;
        }

        /// <summary>
        /// 获取用户排名
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>排名响应</returns>
        public RankingListSingleResponse GetUserRank(int userId)
        {
            var request = new GetUserRankRequest
            {
                UserId = userId
            };

            var response = SendRequestAsync<GetUserRankResponse>(request).Result;
            return response.Result ?? throw new Exception("Failed to get user rank.");
        }

        /// <summary>
        /// 获取排行榜多响应
        /// </summary>
        /// <param name="topN">前N名用户数量</param>
        /// <param name="aroundUserId">周围用户的中心用户ID</param>
        /// <param name="aroundN">周围用户数量</param>
        /// <returns>排行榜多响应</returns>
        public RankingListMutiResponse GetRankingListMutiResponse(int topN, int aroundUserId, int aroundN)
        {
            var request = new GetRankingListMutiResponseRequest
            {
                TopN = topN,
                AroundUserId = aroundUserId,
                AroundN = aroundN
            };

            var response = SendRequestAsync<GetRankingListMutiResponseResponse>(request).Result;
            return response.Result ?? throw new Exception("Failed to get ranking list.");
        }

        /// <summary>
        /// 获取服务器内存使用情况
        /// </summary>
        /// <returns>当前内存使用量和峰值内存使用量（字节）</returns>
        public (long CurrentMemory, long PeakMemory) GetMemoryUsage()
        {
            var request = new GetMemoryUsageRequest();
            var response = SendRequestAsync<GetMemoryUsageResponse>(request).Result;
            return (response.CurrentMemoryUsage, response.PeakMemoryUsage);
        }
        
        /// <summary>
        /// 移除用户
        /// </summary>
        /// <param name="userId">用户ID</param>
        public void RemoveUser(int userId)
        {
            // RemoveUser is not implemented in the current protocol
            // This is just a placeholder to satisfy the interface
            throw new NotImplementedException("RemoveUser is not implemented in the current protocol.");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _isRunning = false;
                _readThread.Join();

                lock (_lock)
                {
                    if (_pipeStream != null)
                    {
                        _pipeStream.Dispose();
                        _pipeStream = null;
                    }

                    // Complete all pending requests with cancellation
                    foreach (var request in _pendingRequests)
                    {
                        request.Value.SetException(new OperationCanceledException("Client disposed.", new ObjectDisposedException(nameof(RankingListClient))));
                    }
                    _pendingRequests.Clear();
                }

                _disposed = true;
            }
        }
    }
}
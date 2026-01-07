using RankingList;
using RankingListServer.Communication;
using System.Diagnostics;
using System.IO.Pipes;

namespace RankingListServer
{
    class Program
    {
        private static IRankingList? _rankingList;
        private static bool _isRunning = true;
        private static readonly string PipeName = "RankingListPipe";
        private static long _peakMemoryUsage = 0;
        private static readonly object _lock = new();

        static void Main(string[] args)
        {
            Console.WriteLine("=== Ranking List Server ===");
            Console.WriteLine("Starting server...");

            // Initialize ranking list
            InitializeRankingList();

            // Start memory monitoring thread
            var memoryThread = new Thread(MonitorMemoryUsage);
            memoryThread.Start();

            // Start named pipe server
            StartNamedPipeServer();

            // Wait for termination
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            // Stop server
            _isRunning = false;
            memoryThread.Join();
            Console.WriteLine("Server stopped.");
        }

        private static void InitializeRankingList()
        {
            // Create empty ranking list
            _rankingList = DllMain.CreateRankingList(Array.Empty<User>());
            Console.WriteLine("Ranking list initialized.");
        }

        private static void StartNamedPipeServer()
        {
            // Start accepting connections in a new thread
            var serverThread = new Thread(AcceptConnections);
            serverThread.Start();
            Console.WriteLine($"Named pipe server started. Pipe name: {PipeName}");
        }

        private static void AcceptConnections()
        {
            while (_isRunning)
            {
                try
                {
                    using var serverStream = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    // Wait for a client to connect
                    serverStream.WaitForConnection();
                    Console.WriteLine("Client connected.");

                    // Handle client communication in a separate thread
                    var clientThread = new Thread(() => HandleClientCommunication(serverStream));
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine($"Error accepting client connection: {ex.Message}");
                    }
                }
            }
        }

        private static void HandleClientCommunication(NamedPipeServerStream serverStream)
        {
            try
            {
                using (serverStream)
                {
                    while (_isRunning && serverStream.IsConnected)
                    {
                        try
                        {
                            // Read request from client
                        Console.WriteLine("Waiting for client request...");
                        var request = ReadRequest(serverStream);
                        if (request == null)
                        {
                            Console.WriteLine("Received null request, closing connection...");
                            break;
                        }

                            Console.WriteLine($"Received request: {request.Type}, RequestId: {request.RequestId}");

                            // Process request
                            var response = ProcessRequest(request);
                            Console.WriteLine($"Processed request, sending response...");

                            // Send response back to client
                            SendResponse(serverStream, response);
                            Console.WriteLine($"Sent response for RequestId: {request.RequestId}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in client communication loop: {ex.Message}");
                            Console.WriteLine(ex.StackTrace);
                        }
                    }
                }
                Console.WriteLine("Client disconnected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client communication: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static RequestBase? ReadRequest(NamedPipeServerStream serverStream)
        {
            try
            {
                // Read message length first
                Console.WriteLine("Reading message length...");
                byte[] lengthBuffer = new byte[4];
                int bytesRead = serverStream.Read(lengthBuffer, 0, 4);
                Console.WriteLine($"Read {bytesRead} bytes for message length");
                if (bytesRead != 4)
                    return null;

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                Console.WriteLine($"Message length: {messageLength}");
                if (messageLength <= 0)
                    return null;

                // Read message content
                Console.WriteLine($"Reading message content of {messageLength} bytes...");
                byte[] messageBuffer = new byte[messageLength];
                bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    int read = serverStream.Read(messageBuffer, bytesRead, messageLength - bytesRead);
                    Console.WriteLine($"Read {read} bytes, total so far: {bytesRead + read}");
                    if (read == 0)
                        return null;
                    bytesRead += read;
                }

                // Deserialize binary request
                Console.WriteLine("Deserializing request...");
                return RequestBase.Deserialize(messageBuffer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading request: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return null;
            }
        }

        private static void SendResponse(NamedPipeServerStream serverStream, ResponseBase response)
        {
            try
            {
                // Serialize response to binary
                byte[] messageBuffer = response.Serialize();
                int messageLength = messageBuffer.Length;

                // Write message length first
                byte[] lengthBuffer = BitConverter.GetBytes(messageLength);
                serverStream.Write(lengthBuffer, 0, 4);

                // Write message content
                serverStream.Write(messageBuffer, 0, messageLength);
                serverStream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending response: {ex.Message}");
            }
        }

        private static ResponseBase ProcessRequest(RequestBase request)
        {
            if (_rankingList == null)
            {
                // Return an appropriate response type based on request type
                switch (request.Type)
                {
                    case RequestType.Initialize:
                        return new InitializeResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = "Ranking list not initialized."
                        };
                    case RequestType.AddOrUpdateUser:
                        return new AddOrUpdateUserResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = "Ranking list not initialized."
                        };
                    case RequestType.GetUserRank:
                        return new GetUserRankResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = "Ranking list not initialized."
                        };
                    case RequestType.GetRankingListMutiResponse:
                        return new GetRankingListMutiResponseResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = "Ranking list not initialized."
                        };
                    case RequestType.GetMemoryUsage:
                        return new GetMemoryUsageResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = "Ranking list not initialized."
                        };
                    default:
                        return new AddOrUpdateUserResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = "Ranking list not initialized."
                        };
                }
            }

            try
            {
                switch (request.Type)
                {
                    case RequestType.Initialize:
                        return ProcessInitializeRequest(request as InitializeRequest);
                    case RequestType.AddOrUpdateUser:
                        return ProcessAddOrUpdateUserRequest(request as AddOrUpdateUserRequest);
                    case RequestType.GetUserRank:
                        return ProcessGetUserRankRequest(request as GetUserRankRequest);
                    case RequestType.GetRankingListMutiResponse:
                        return ProcessGetRankingListMutiResponseRequest(request as GetRankingListMutiResponseRequest);
                    case RequestType.GetMemoryUsage:
                        return ProcessGetMemoryUsageRequest(request as GetMemoryUsageRequest);
                    default:
                        return new AddOrUpdateUserResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = $"Unknown request type: {request.Type}"
                        };
                }
            }
            catch (Exception ex)
            {
                // Return an appropriate response type based on request type
                switch (request.Type)
                {
                    case RequestType.AddOrUpdateUser:
                        return new AddOrUpdateUserResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    case RequestType.GetUserRank:
                        return new GetUserRankResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    case RequestType.GetRankingListMutiResponse:
                        return new GetRankingListMutiResponseResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    case RequestType.GetMemoryUsage:
                        return new GetMemoryUsageResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    default:
                        return new AddOrUpdateUserResponse(request.RequestId)
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                }
            }
        }

        private static AddOrUpdateUserResponse ProcessAddOrUpdateUserRequest(AddOrUpdateUserRequest? request)
        {
            if (request == null || _rankingList == null)
            {
                return new AddOrUpdateUserResponse(request?.RequestId ?? Guid.Empty)
                {
                    Success = false,
                    ErrorMessage = "Invalid request."
                };
            }

            _rankingList.AddOrUpdateUser(request.UserId, request.Score, request.LastActive);
            return new AddOrUpdateUserResponse(request.RequestId);
        }

        private static GetUserRankResponse ProcessGetUserRankRequest(GetUserRankRequest? request)
        {
            if (request == null || _rankingList == null)
            {
                return new GetUserRankResponse(request?.RequestId ?? Guid.Empty)
                {
                    Success = false,
                    ErrorMessage = "Invalid request."
                };
            }

            var result = _rankingList.GetUserRank(request.UserId);
            var response = new GetUserRankResponse(request.RequestId)
            {
                Result = result
            };
            return response;
        }

        private static GetRankingListMutiResponseResponse ProcessGetRankingListMutiResponseRequest(GetRankingListMutiResponseRequest? request)
        {
            if (request == null || _rankingList == null)
            {
                return new GetRankingListMutiResponseResponse(request?.RequestId ?? Guid.Empty)
                {
                    Success = false,
                    ErrorMessage = "Invalid request."
                };
            }

            var result = _rankingList.GetRankingListMutiResponse(request.TopN, request.AroundUserId, request.AroundN);
            var response = new GetRankingListMutiResponseResponse(request.RequestId)
            {
                Result = result
            };
            return response;
        }

        private static GetMemoryUsageResponse ProcessGetMemoryUsageRequest(GetMemoryUsageRequest? request)
        {
            if (request == null)
            {
                return new GetMemoryUsageResponse(request?.RequestId ?? Guid.Empty)
                {
                    Success = false,
                    ErrorMessage = "Invalid request."
                };
            }

            long currentMemory = Process.GetCurrentProcess().WorkingSet64;
            lock (_lock)
            {
                if (currentMemory > _peakMemoryUsage)
                {
                    _peakMemoryUsage = currentMemory;
                }
            }

            var response = new GetMemoryUsageResponse(request.RequestId)
            {
                CurrentMemoryUsage = currentMemory,
                PeakMemoryUsage = _peakMemoryUsage
            };
            return response;
        }

        private static InitializeResponse ProcessInitializeRequest(InitializeRequest? request)
        {
            if (request == null)
            {
                return new InitializeResponse(request?.RequestId ?? Guid.Empty)
                {
                    Success = false,
                    ErrorMessage = "Invalid request."
                };
            }

            _rankingList = DllMain.CreateRankingList(request.Users ?? Array.Empty<User>());
            return new InitializeResponse(request.RequestId);
        }

        private static void MonitorMemoryUsage()
        {
            while (_isRunning)
            {
                try
                {
                    long currentMemory = Process.GetCurrentProcess().WorkingSet64;
                    lock (_lock)
                    {
                        if (currentMemory > _peakMemoryUsage)
                        {
                            _peakMemoryUsage = currentMemory;
                        }
                    }
                    Thread.Sleep(100); // Check every 100ms
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring memory: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }
    }
}

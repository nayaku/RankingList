using System.Diagnostics;
using RankingList;

namespace RankingListTest
{
    public class RankingListConcurrentTest
    {
        private readonly IRankingList _rankingList;
        private readonly int _totalOperations;
        private readonly int _concurrencyLevel;
        private readonly int _initialUserCount;
        
        private int _currentConcurrency;
        private int _maxConcurrency;
        private long _totalResponseTime;
        private int _completedOperations;
        private readonly object _lock = new object();
        private int _nextUserId;
        private long _peakMemoryUsage = 0;
        private Thread? _memoryMonitorThread;
        private bool _isMemoryMonitoring = false;
        private Process? _process;
        
        public RankingListConcurrentTest(int initialUserCount, int totalOperations, int concurrencyLevel)
        {
            _initialUserCount = initialUserCount;
            _totalOperations = totalOperations;
            _concurrencyLevel = concurrencyLevel;
            
            // Start ranking list server process
            StartRankingListServer();
            
            // Wait a moment for server to start
            Thread.Sleep(2000);
            
            // Create initial users with power-law distributed scores
            var initialUsers = new List<User>();
            for (int i = 1; i <= initialUserCount; i++)
            {
                initialUsers.Add(new User
                {
                    ID = i,
                    Score = GeneratePowerLawScore(),
                    LastActive = DateTime.Now
                });
            }
            
            // Use DllMain::CreateRankingList to get ranking list instance
            _rankingList = DllMain.CreateRankingList(initialUsers);
            _nextUserId = initialUserCount + 1;
        }
        
        /// <summary>
        /// Start ranking list server process
        /// </summary>
        private void StartRankingListServer()
        {
            try
            {
                string serverPath = Path.Combine("RankingListServer.exe");
                if (File.Exists(serverPath))
                {
                    Console.WriteLine($"Starting ranking list server from: {serverPath}");
                    _process = Process.Start(serverPath);
                    Console.WriteLine($"Server started with PID: {_process.Id}");
                }
                else
                {
                    Console.WriteLine($"Server executable not found at: {serverPath}");
                    Console.WriteLine("Using in-process ranking list instead.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start server: {ex.Message}");
                Console.WriteLine("Using in-process ranking list instead.");
            }
        }
        
        // Generate power-law distributed scores (more low scores, fewer high scores)
        private int GeneratePowerLawScore()
        {
            // Parameters for power-law distribution: base score range 1-10000
            // Using exponent -2 for a steep distribution
            var random = new Random(Guid.NewGuid().GetHashCode());
            double uniform = random.NextDouble();
            // Power-law transformation: score = minScore + (maxScore - minScore) * (1 - uniform)^(1/exponent)
            int minScore = 1;
            int maxScore = 10000;
            double exponent = -2.0;
            return (int)(minScore + (maxScore - minScore) * Math.Pow(1 - uniform, 1 / exponent));
        }
        
        public void RunTest()
        {
            Console.WriteLine("Starting concurrent test...");
            Console.WriteLine($"Initial users: {_initialUserCount}");
            Console.WriteLine($"Total operations: {_totalOperations}");
            Console.WriteLine($"Concurrency level: {_concurrencyLevel}");
            
            // Start memory monitoring
            StartMemoryMonitoring();
            
            var sw = Stopwatch.StartNew();
            
            // Create and start tasks
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrencyLevel; i++)
            {
                tasks.Add(Task.Run(() => PerformOperations()));
            }
            
            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());
            
            sw.Stop();
            
            // Stop memory monitoring
            StopMemoryMonitoring();
            
            // Calculate results
            double averageResponseTime = (double)_totalResponseTime / _completedOperations;
            double throughput = _completedOperations / sw.Elapsed.TotalSeconds;
            
            // Get final peak memory usage
            long finalPeakMemory = _peakMemoryUsage;
            
            Console.WriteLine("\nTest Results:");
            Console.WriteLine($"Total time elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Completed operations: {_completedOperations}");
            Console.WriteLine($"Maximum concurrent operations: {_maxConcurrency}");
            Console.WriteLine($"Average response time: {averageResponseTime:F2} ms");
            Console.WriteLine($"Throughput: {throughput:F2} operations/second");
            Console.WriteLine($"Peak memory usage: {FormatMemory(finalPeakMemory)}");
            
            // Stop the server process if it was started
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                Console.WriteLine($"Server process with PID {_process.Id} stopped.");
            }
        }
        
        /// <summary>
        /// Start memory monitoring thread
        /// </summary>
        private void StartMemoryMonitoring()
        {
            _isMemoryMonitoring = true;
            _memoryMonitorThread = new Thread(MonitorMemoryUsage);
            _memoryMonitorThread.Start();
        }
        
        /// <summary>
        /// Stop memory monitoring thread
        /// </summary>
        private void StopMemoryMonitoring()
        {
            _isMemoryMonitoring = false;
            if (_memoryMonitorThread != null && _memoryMonitorThread.IsAlive)
            {
                _memoryMonitorThread.Join();
            }
        }
        
        /// <summary>
        /// Monitor memory usage periodically
        /// </summary>
        private void MonitorMemoryUsage()
        {
            while (_isMemoryMonitoring)
            {
                try
                {
                    // Get current process memory usage
                    long currentMemory = Process.GetCurrentProcess().WorkingSet64;
                    if (currentMemory > _peakMemoryUsage)
                    {
                        _peakMemoryUsage = currentMemory;
                    }
                    Thread.Sleep(100); // Check every 100ms
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring memory: {ex.Message}");
                    Thread.Sleep(1000); // Wait longer before next try if error occurs
                }
            }
        }
        
        /// <summary>
        /// Format memory size for display
        /// </summary>
        /// <param name="bytes">Memory size in bytes</param>
        /// <returns>Formatted memory size string</returns>
        private string FormatMemory(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
        
        private void PerformOperations()
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            int operationsPerTask = _totalOperations / _concurrencyLevel;
            
            for (int i = 0; i < operationsPerTask; i++)
            {
                double operationType = random.NextDouble();
                
                if (operationType < 0.7) // 70% GetRankingListMutiResponse
                {
                    PerformGetRankingOperation(random);
                }
                else if (operationType < 0.9) // 20% AddOrUpdateUser
                {
                    PerformAddOrUpdateOperation(random);
                }
                else // 10% GetUserRank
                {
                    PerformGetUserRankOperation(random);
                }
            }
        }
        
        private void PerformGetRankingOperation(Random random)
        {
            // Get a random user ID from existing users
            int userId = random.Next(1, _nextUserId);
            int topN = random.Next(5, 20);
            int aroundN = random.Next(3, 10);
            
            // Record start time and increment current concurrency
            int currentConcurrency = Interlocked.Increment(ref _currentConcurrency);
            
            // Update max concurrency
            lock (_lock)
            {
                if (currentConcurrency > _maxConcurrency)
                {
                    _maxConcurrency = currentConcurrency;
                }
            }
            
            var sw = Stopwatch.StartNew();
            
            // Perform the operation
            _rankingList.GetRankingListMutiResponse(topN, userId, aroundN);
            
            sw.Stop();
            
            // Decrement current concurrency and update metrics
            Interlocked.Decrement(ref _currentConcurrency);
            Interlocked.Add(ref _totalResponseTime, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _completedOperations);
        }
        
        private void PerformAddOrUpdateOperation(Random random)
        {
            // 30% chance to add new user, 70% chance to update existing user
            bool isNewUser = random.NextDouble() < 0.3;
            
            int userId;
            int score;
            
            if (isNewUser)
            {
                // Add new user
                userId = Interlocked.Increment(ref _nextUserId);
                score = GeneratePowerLawScore();
            }
            else
            {
                // Update existing user score
                userId = random.Next(1, _nextUserId);
                // Generate a higher score than current
                // First, let's get the current score (we'll simulate this since we don't have a direct method)
                // For the purpose of testing, we'll just generate a new score that's higher than the previous one
                // In real scenario, we would need to track user scores or modify the interface
                score = GeneratePowerLawScore() + random.Next(1, 1000);
            }
            
            // Record start time and increment current concurrency
            int currentConcurrency = Interlocked.Increment(ref _currentConcurrency);
            
            // Update max concurrency
            lock (_lock)
            {
                if (currentConcurrency > _maxConcurrency)
                {
                    _maxConcurrency = currentConcurrency;
                }
            }
            
            var sw = Stopwatch.StartNew();
            
            // Perform the operation
            _rankingList.AddOrUpdateUser(userId, score, DateTime.Now);
            
            sw.Stop();
            
            // Decrement current concurrency and update metrics
            Interlocked.Decrement(ref _currentConcurrency);
            Interlocked.Add(ref _totalResponseTime, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _completedOperations);
        }
        
        private void PerformGetUserRankOperation(Random random)
        {
            // Get a random user ID from existing users
            int userId = random.Next(1, _nextUserId);
            
            // Record start time and increment current concurrency
            int currentConcurrency = Interlocked.Increment(ref _currentConcurrency);
            
            // Update max concurrency
            lock (_lock)
            {
                if (currentConcurrency > _maxConcurrency)
                {
                    _maxConcurrency = currentConcurrency;
                }
            }
            
            var sw = Stopwatch.StartNew();
            
            // Perform the operation
            _rankingList.GetUserRank(userId);
            
            sw.Stop();
            
            // Decrement current concurrency and update metrics
            Interlocked.Decrement(ref _currentConcurrency);
            Interlocked.Add(ref _totalResponseTime, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _completedOperations);
        }
    }
}
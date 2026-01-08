// namespace RankingList
// {
//     internal class SimpleRankingList : IRankingList
//     {
//         public List<User> Users { get; set; }

//         public SimpleRankingList(User[] users)
//         {
//             Users = [.. users];
//             Users.Sort();
//         }
//         public void AddOrUpdateUser(User user)
//         {
//             var existingUser = Users.FirstOrDefault(u => u.ID == user.ID);
//             if (existingUser != null)
//             {
//                 Users.Remove(existingUser);
//             }
//             Users.Add(user);
//             Users.Sort();
//         }
//         public void AddOrUpdateUser(int userId, int score, DateTime lastActive)
//         {
//             var user = new User
//             {
//                 ID = userId,
//                 Score = score,
//                 LastActive = lastActive
//             };
//             AddOrUpdateUser(user);
//         }
//         public RankingListSingleResponse GetUserRank(int userId)
//         {
//             var index = Users.FindIndex(u => u.ID == userId);
//             if (index == -1) return null;
//             return new RankingListSingleResponse
//             {
//                 User = Users[index],
//                 Rank = index + 1
//             };
//         }
//         public RankingListMutiResponse GetRankingListMutiResponse(int topN, int aroundUserId, int aroundN)
//         {
//             var response = new RankingListMutiResponse
//             {
//                 TotalUsers = Users.Count
//             };
//             // Top N users
//             var topNNum = Math.Min(topN, Users.Count);
//             response.TopNUsers = new RankingListSingleResponse[topNNum];
//             for (int i = 0; i < topNNum; i++)
//             {
//                 response.TopNUsers[i] = new RankingListSingleResponse
//                 {
//                     User = Users[i],
//                     Rank = i + 1
//                 };
//             }
//             // Users around a specific user
//             var index = Users.FindIndex(u => u.ID == aroundUserId);
//             if (index != -1)
//             {
//                 int start = Math.Max(0, index - aroundN);
//                 int end = Math.Min(Users.Count - 1, index + aroundN);
//                 int count = end - start + 1;
//                 response.RankingAroundUsers = new RankingListSingleResponse[count];
//                 for (int i = start; i <= end; i++)
//                 {
//                     response.RankingAroundUsers[i - start] = new RankingListSingleResponse
//                     {
//                         User = Users[i],
//                         Rank = i + 1
//                     };
//                 }
//             }
//             return response;
//         }
//     }
// }
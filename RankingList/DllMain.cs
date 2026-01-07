namespace RankingList
{
    public static class DllMain
    {
        public static IRankingList CreateRankingList(User[] users, string rankingListName = "SimpleRankingList")
        {
            switch (rankingListName)
            {
                case "SimpleRankingList":
                    return new SimpleRankingList(users);
                case "SimpleRankingList2":
                    return new SimpleRankingList2(users);
                case "EmptyRankingList":
                    return new EmptyRankingList();
                // 可以添加更多排行榜实现
                default:
                    throw new ArgumentException($"未知的排行榜类型: {rankingListName}");
            }
        }
    }
}

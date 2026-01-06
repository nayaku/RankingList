namespace RankingList
{
    public static class DllMain
    {
        public static IRankingList CreateRankingList(List<User> users)
        //{
        {
            //return new SimpleRankingList(users);
            return new SimpleRankingList2(users);
        }
    }
}

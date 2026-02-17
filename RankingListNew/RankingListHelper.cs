namespace RankingListNew
{
    public static class RankingListHelper
    {
        public static IRankingList NewRankingList(string rankingListClassName)
        {
            var rankingListType = Type.GetType($"RankingListNew.{rankingListClassName}")??
                                  throw new ArgumentException($"RankingList class {rankingListClassName} not found");
            if (Activator.CreateInstance(rankingListType) is not IRankingList rankingList)
            {
                throw new ArgumentException($"RankingList class {rankingListClassName} not found");
            }
            return rankingList;
        }
    }
}

namespace RankingListNew
{
    public static class RankingListHelper
    {
        public static IRankingList NewRankingList(string rankingListClassName, object? parameter = null)
        {
            Type rankingListType = Type.GetType($"RankingListNew.{rankingListClassName}") ??
                                  throw new ArgumentException($"RankingList class {rankingListClassName} not found");
            if (Activator.CreateInstance(rankingListType, parameter) is not IRankingList rankingList)
            {
                throw new ArgumentException($"RankingList class {rankingListClassName} not found");
            }
            return rankingList;
        }
    }
}

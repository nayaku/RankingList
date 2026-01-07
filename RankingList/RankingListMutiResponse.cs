namespace RankingList
{
    public class RankingListMutiResponse
    {
        public RankingListSingleResponse[] TopNUsers { get; set; }
        public RankingListSingleResponse[] RankingAroundUsers { get; set; }
        public int TotalUsers { get; set; }
    }
}

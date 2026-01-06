namespace RankingList
{
    public class RankingListMutiResponse
    {
        public List<RankingListSingleResponse> TopNUsers { get; set; }
        public List<RankingListSingleResponse> RankingAroundUsers { get; set; }
        public int TotalUsers { get; set; }
    }
}

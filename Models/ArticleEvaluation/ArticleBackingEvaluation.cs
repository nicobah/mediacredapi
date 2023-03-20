namespace MediaCred.Models.ArticleEvaluation
{
    public class ArticleBackingEvaluation
    {
        public string Name { get; set; } = "ArticleUsedAsBacking";
        public string Description { get; set; } = "Evaluates the article based on how many claims use it as backing.";

        public async Task<double> GetEvaluation(Article art)
        {
            //Should probably be modified to check if the claim is within the same topic/area as the article, so only relevant backings count
            var resultCalc = 0.0;

            if (art.UsedAsBacking != null)
            {
                resultCalc = art.UsedAsBacking.Value * 50;
            }

            return resultCalc > 100.0 ? 100.0 : resultCalc;
        }
    }
}

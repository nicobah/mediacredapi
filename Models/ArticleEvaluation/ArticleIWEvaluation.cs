namespace MediaCred.Models.ArticleEvaluation
{
    public class ArticleIWEvaluation : IArticleCredibilityEvaluation
    {
        public string Name { get; set; } = "ArticleInappropriateWords";
        public string Description { get; set; } = "Evaluates the article based on the number of innappropriate words (IW). Having 0 IWs yields the maximum score, the first IW subtracts 30% from the score and any further IWs subtract another 10% per IW.";

        public async Task<double> GetEvaluation(Article art)
        {
            var iwCount = art.InappropriateWords.HasValue ? art.InappropriateWords : 0;
            
            //Calculates the score based on the number of IWs.
            //Each IW lowers the score by 10% and having any IWs subtracts another 20%
            //This means that the first IW will cost 30% and then 10% more per IW.
            var resultCalc = iwCount > 0 ? 100 - 20 - (10*iwCount) : 100;
            //To-do: Take 10% of the current value for each subsequent IW.

            return resultCalc >= 0 ? (double)resultCalc : 0.0;
        }
    }
}

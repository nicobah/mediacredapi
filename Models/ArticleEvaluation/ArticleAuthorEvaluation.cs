using MediaCred.Models.Services;

namespace MediaCred.Models.ArticleEvaluation
{
    public class ArticleAuthorEvaluation : IArticleCredibilityEvaluation
    {
        public string Name { get; set; } = "ArticleAuthorCred";
        public string Description { get; set; } = "Evaluates the article based on the authors of it.";

        public async Task<double> GetEvaluation(Article art)
        {
            var resultCalc = 0.0;

            if (art.Authors != null && art.Authors.Count > 0)
            {
                resultCalc = art.Authors.Where(x => x.Credibility != null).Sum(x => x.Credibility.Value);
            }

            return resultCalc > 0.0 && art.Authors != null ? resultCalc / art.Authors.Count : 0.0;
        }
    }
}

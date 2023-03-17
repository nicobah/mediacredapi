namespace MediaCred.Models.ArticleEvaluation
{
    public class ArticleRefEvaluation : IArticleCredibilityEvaluation
    {
        public string Name { get; set; } = "ArticleReferences";
        public string Description { get; set; } = "Evaluates the article based on the number of references. Having 1 reference gives 50% score, having 2 or more gives 100%. Will be expanded to take each references score into account.";

        public double GetEvaluation(Article art)
        {
            //TO-DO:
            //Should be expanded to be a list of articles and get their credibility to calculate score.
            var refCount = art.References.HasValue ? art.References : 0;
            
            //Calculates the score based on the number of references.
            //Having 1 ref gives 50%, having 2 or more references yields the maximum score.
            var resultCalc = 50*refCount;

            return resultCalc > 100 ? 100.0 : (double)resultCalc;
        }
    }
}

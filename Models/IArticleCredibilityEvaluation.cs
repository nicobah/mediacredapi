namespace MediaCred.Models
{
    public interface IArticleCredibilityEvaluation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        Func<Article, double> GetEvaluation();
    }
}

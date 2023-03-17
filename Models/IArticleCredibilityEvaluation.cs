namespace MediaCred.Models
{
    public interface IArticleCredibilityEvaluation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Task<double> GetEvaluation(Article art);
    }
}

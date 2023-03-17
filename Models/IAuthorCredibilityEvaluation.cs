namespace MediaCred.Models
{
    public interface IAuthorCredibilityEvaluation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Task<double> GetEvaluation(Author auth);
    }
}

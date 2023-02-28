namespace MediaCred.Models
{
    public interface IAuthorCredibilityEvaluation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        Func<Author, double> GetEvaluation();
    }
}

namespace MediaCred.Models
{
    public class AuthorInformationEvaluation : IAuthorCredibilityEvaluation
    {
        public string Name { get; set; } = "AuthorInformation";
        public string Description { get; set; } = "Evaluates the author based on the number of basic information provided about the author, if name, age, education political orientation etc is provided it will yield the max score";

        public async Task<double> GetEvaluation(Author auth)
        {
            var propCount = typeof(Author).GetProperties().Length;
            //Calculate the weight for each property based on the number of properties in an author
            double propertiesWeight = (double)100 / (double)propCount;
            double result = 0;
            foreach (var prop in auth.GetType().GetProperties())
            {
                if (prop.GetValue(auth) != null)
                {
                    {
                        result += propertiesWeight;
                    }
                }
            }
            return Math.Round(result, 2);
        }

    
    }
}

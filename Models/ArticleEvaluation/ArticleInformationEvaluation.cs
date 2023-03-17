﻿namespace MediaCred.Models.ArticleEvaluation
{
    public class ArticleInformationEvaluation : IArticleCredibilityEvaluation
    {
        public string Name { get; set; } = "ArticleInformation";
        public string Description { get; set; } = "Evaluates the article based on the number of evaluation parameters provided about the article, if title, publsiher, link, references, # of innappropriate words and level of content similarity is provided it will yield the max score";

        public double GetEvaluation(Article art)
        {
            var propCount = typeof(Article).GetProperties().Length;
            //Calculate the weight for each property based on the number of properties in an author
            double propertiesWeight = 100 / (double)propCount;
            double result = 0;
            foreach (var prop in art.GetType().GetProperties())
            {
                if (prop.GetValue(art) != null)
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

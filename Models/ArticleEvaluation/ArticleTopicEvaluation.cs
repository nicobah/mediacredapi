using MediaCred.Models.Services;
using Neo4j.Driver;

namespace MediaCred.Models.ArticleEvaluation
{
    public class ArticleTopicEvaluation : IArticleCredibilityEvaluation
    {
        public string Name { get; set; } = "ArticleTopicExpertise";
        public string Description { get; set; } = "Evaluates the article based on the indicated topic and how it relates to the authors area of expertise.";

        public async Task<double> GetEvaluation(Article art)
        {
            var resultCalc = 0;

            if (art.Topic != null)
            {
                var qs = new QueryService();
                var author = await qs.GetAuthorByID(art.AuthorID);
                
                if(author != null && author.AreaOfExpertise != null && author.AreaOfExpertise.Length > 0) 
                {
                    var topicKeyWords = art.Topic.Split(" ");

                    var authorExpertiseKeywords = author.AreaOfExpertise.Split(" ");

                    foreach(var word in authorExpertiseKeywords)
                    {
                        foreach(var tWord in topicKeyWords)
                        {
                            //If a word in the topic contains some form of a word from the authors area of expertise, increase score by 10
                            //This enables cases like topic = Java, Author AoE = Java-based (good)
                            //But also enables cases like topic = Java, Author AoE = JavaScript (bad)
                            //This is a crude implementation, so it would preferably be improved.
                            if (tWord.ToLower().Contains(word.ToLower()))    
                            {
                                resultCalc += 10;
                            }
                        }
                    }
                }
            }

            return resultCalc > 100 ? 100 : resultCalc;
        }
    }
}

using MediaCred.Models.Services;
using Neo4j.Driver;

namespace MediaCred.Models.ArticleEvaluation
{
    public class ArticleTopicEvaluation : IArticleCredibilityEvaluation
    {
        public string Name { get; set; } = "ArticleTopicExpertise";
        public string Description { get; set; } = "Evaluates the article based on the indicated topic and how it relates to the authors area of expertise. For each author it checks how many topics correlate to the author expertise, then finally it takes the average score of all the authors.";

        public async Task<double> GetEvaluation(Article art)
        {
            var resultCalc = 0.0;

            if (art.Topic != null)
            {
                var qs = new QueryService();
                var authorList = await qs.GetAuthorsByArticleID(art.ID);

                if(authorList != null && authorList.Count > 0) {
                    var topicKeyWords = art.Topic.Split(",");
                    try
                    {
                        foreach (var author in authorList)
                        {
                            if (author != null && author.AreaOfExpertise != null && author.AreaOfExpertise.Length > 0)
                            {
                                var authorExpertiseKeywords = author.AreaOfExpertise.Split(",");

                                var authorCalc = 0.0;

                                foreach (var word in authorExpertiseKeywords)
                                {
                                    word.Trim();

                                    foreach (var tWord in topicKeyWords)
                                    {
                                        //If a word in the topic contains some form of a word from the authors area of expertise, increase score by 10
                                        //This enables cases like topic = Java, Author AoE = Java-based (good)
                                        //But also enables cases like topic = Java, Author AoE = JavaScript (bad)
                                        //This is a crude implementation, so it would preferably be improved.
                                        tWord.Trim();
                                        if (tWord.ToLower().Contains(word.ToLower()))
                                        {
                                            authorCalc += 1;
                                        }
                                    }
                                }
                                if (authorCalc > 0)
                                    resultCalc += (authorCalc / topicKeyWords.Count()) * 100;
                            }
                        }

                        resultCalc = resultCalc / authorList.Count();
                    }
                    catch { }
                }
            }
            return resultCalc > 100 ? 100 : resultCalc;
        }
    }
}

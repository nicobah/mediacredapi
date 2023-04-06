using MediaCred.Models.Services;

namespace MediaCred.Models
{
    public class User
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public DateTime NextNudge { get; set; }
        public int PoliticalBias { get; set; } = 1;
        public List<Article>? SubscribesTo { get; set; }
        public List<string>? ArticlesRead { get; set; }
        public double NotificationThreshold { get; set; } = 1;
        public double InformationWeight { get; set; } = 1;
        public double InappropriateWordsWeight { get; set; } = 1;
        public double ReferencesWeight { get; set; } = 1;
        public double TopicWeight { get; set; } = 1;
        public double AuthorWeight { get; set; } = 1;
        public double BackingsWeight { get; set; } = 1;


        public string GetOppositeBias()
        {
            List<string> strings = new List<string> { "left", "right" };
           
            if (PoliticalBias > 0)
            {
                return strings.FirstOrDefault();
            } else if(PoliticalBias < 0)
            {
                return strings.LastOrDefault();
            }
            Random random = new Random();
            int index = random.Next(0, 2); // generates a random number between 0 and 1 (inclusive)
            return strings[index];
        }

        public async Task<string> GetLatestTopic(QueryService qs)
        {
            foreach (var artId in ArticlesRead)
            {
                var art = await qs.GetArticleById(artId);
                if(art.Topic != null)
                {
                    return art.Topic;
                }
                else
                {
                    continue;
                }
            }
            return "topic";
        }
    }
}

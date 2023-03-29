namespace MediaCred.Models
{
    public class User
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public List<Article>? SubscribesTo { get; set; }
        public double NotificationThreshold { get; set; } = 1;
        public double InformationWeight { get; set; } = 1;
        public double InappropriateWordsWeight { get; set; } = 1;
        public double ReferencesWeight { get; set; } = 1;
        public double TopicWeight { get; set; } = 1;
        public double AuthorWeight { get; set; } = 1;
        public double BackingsWeight { get; set; } = 1;

    }
}

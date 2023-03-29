namespace MediaCred.Models
{
    public class UserSettings
    {
        public int ID { get; set; }
        public double NotificationThreshold { get; set; }
        public double InformationWeight { get; set; }
        public double InappropriateWordsWeight { get; set; }
        public double ReferencesWeight { get; set; }
        public double TopicWeight { get; set; }
        public double AuthorWeight { get; set; }
        public double BackingsWeight { get; set; }

    }
}

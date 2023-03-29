namespace MediaCred.Models
{
    public class User
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public List<Article> SubscribesTo { get; set; }
        public UserSettings UserSettings { get; set; }

    }
}

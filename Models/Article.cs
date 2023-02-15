namespace MediaCred
{
    public class Article
    {
        public string Title { get; set; }
        public string? Publisher { get; set; }

        public string? Link { get; set; }

        public override string ToString()
        {
            return "Title: " + this.Title + " Publisher: " + this.Publisher + " Link: " + this.Link;
        }
    }
}

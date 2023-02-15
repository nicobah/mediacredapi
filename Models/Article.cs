using MediaCred.Models;

namespace MediaCred
{
    public class Article : Node
    {
        public string Title { get; set; }
        public string? Publisher { get; set; }

        public string? Link { get; set; }

        public override string GetFullString()
        {
            return "Title: " + this.Title + " Publisher: " + this.Publisher + " Link: " + this.Link;
        }

        public override string ToString()
        {
            return this.Title;
        }
    }
}

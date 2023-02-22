using MediaCred.Models;

namespace MediaCred
{
    public class Article : Node
    {
        public string Title { get; set; }
        public string? Publisher { get; set; }

        public string? Link { get; set; }

        public int? Credibility { get; set; }

        public override string GetFullString()
        {
            return "Title: " + this.Title + " Publisher: " + this.Publisher + " Link: " + this.Link + " Credibility: " + this.Credibility;
        }

        public override string ToString()
        {
            return this.Title;
        }
    }
}

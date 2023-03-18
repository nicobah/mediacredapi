using MediaCred.Models;

namespace MediaCred
{
    public class Article : Node
    {
        public string Title { get; set; }

        public string AuthorID { get; set; }

        public string? Publisher { get; set; }
        public string? Link { get; set; }

        public int? InappropriateWords { get; set; }

        public int? Credibility { get; set; }

        public int? References { get; set; } //Should probably be list of articles?

        public string? Topic { get; set; }

        public override string GetFullString()
        {
            return "Title: " + this.Title + " Publisher: " + this.Publisher + " Link: " + this.Link + " # of inappropriate words: " + this.InappropriateWords + " Credibility: " + this.Credibility;
        }

        public override string ToString()
        {
            return this.Title;
        }
    }
}

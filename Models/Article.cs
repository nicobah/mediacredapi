using MediaCred.Models;

namespace MediaCred
{
    public class Article : Node
    {
        public string Title { get; set; }

        public string? Publisher { get; set; }
        public string? Link { get; set; }

        public int? InappropriateWords { get; set; }
        public string? PoliticalBias { get; set; } //Should be deleted?

        public double? Credibility { get; set; }

        public int? References { get; set; } //Should probably be list of articles, but not for the prototype

        public string? Topic { get; set; }

        public List<Author>? Authors { get; set; } //Not mapped in DB

        public int? UsedAsBacking { get; set; } //Not mapped in DB

        public List<Argument>? Arguments { get; set; } //Not mapped in DB

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

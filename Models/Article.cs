﻿using MediaCred.Models;

namespace MediaCred
{
    public class Article : Node
    {
        public string ID { get; set; }

        public string Title { get; set; }

        public string AuthorID { get; set; }

        public string? Publisher { get; set; }
        public string? Link { get; set; }

        public int? InappropriateWords { get; set; }

        public double? Credibility { get; set; }

        public int? References { get; set; } //Should probably be list of articles?

        public string? Topic { get; set; }

        public List<Author>? Authors { get; set; }

        public int? UsedAsBacking { get; set; }

        public List<Argument>? Arguments { get; set; }

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

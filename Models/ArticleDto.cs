namespace MediaCred.Models
{
    public class ArticleDto
    {
        public string? ID { get; set; }
        public string? Title { get; set; }

        public string? Publisher { get; set; }
        public string? Link { get; set; }

        public int? InappropriateWords { get; set; }

        public double? Credibility { get; set; }

        public int? References { get; set; } //Should probably be list of articles?

        public string? Topic { get; set; }

        public int? UsedAsBacking { get; set; }
    }
}

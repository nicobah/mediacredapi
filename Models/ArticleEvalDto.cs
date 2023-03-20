namespace MediaCred.Models
{
    public class ArticleEvalDto
    {
        public string ArticleLink { get; set; }
        public List<KeyValue> ArticleEvals { get; set; }

        public List<KeyValue>? AuthorEvals { get; set; }
    }
}

using MediaCred.Controllers;
using Neo4j.Driver;
using Newtonsoft.Json;

namespace MediaCred.Models.Services
{
    public class QueryService
    {
        private bool _disposed = false;
        private readonly IDriver _driver;
        private readonly ILogger<MediaCredAPIController>? _logger;

        public QueryService(bool disposed, ILogger<MediaCredAPIController> logger)
        {
            _disposed = disposed;
            _driver = GraphDatabase.Driver("neo4j+s://64d3b06c.databases.neo4j.io", AuthTokens.Basic("neo4j", "k7by2DDGbQvb98r5geSqJMLf1TRBlL_EWeGHqhrxn8M")); ;
            _logger = logger;
        }

        public QueryService()
        {
            _driver = GraphDatabase.Driver("neo4j+s://64d3b06c.databases.neo4j.io", AuthTokens.Basic("neo4j", "k7by2DDGbQvb98r5geSqJMLf1TRBlL_EWeGHqhrxn8M")); ;
        }

        public async Task<List<IRecord>> ExecuteQuery(string query, object parameters = null)
        {
            await using var session = _driver.AsyncSession(configBuilder => configBuilder.WithDatabase("neo4j"));
            try
            {
                // Write transactions allow the driver to handle retries and transient error
                var writeResults = await session.ExecuteWriteAsync(async tx =>
                {
                    var result = await tx.RunAsync(query, parameters);
                    return await result.ToListAsync();
                });

                //var relation = results.FirstOrDefault(x => x.OriginNode is Article && x.IsLeftToRight && x.FinalNode is Argument);
                //var article = relation != null ? relation.OriginNode as Article : new Article { Title = "not found" };
                return writeResults;
            }
            // Capture any errors along with the query and data for traceability
            catch (Neo4jException ex)
            {
                Console.WriteLine($"{query} - {ex}");
                throw;
            }
        }

        public async Task<Article?> GetArticleByLink(string url)
        {
            var query = @"MATCH (art:Article{link:$url})
                            RETURN art";

            var results = await ExecuteQuery(query, new { url });

            var queryAuthors = @"MATCH (art:Article{link:$url})-[:WRITTEN_BY]->(aut:Author)
                            RETURN aut";

            var resultAuthors = await ExecuteQuery(queryAuthors, new { url });

            var queryUsedAsBacking = @"MATCH (art:Article{link:$url})<-[:BACKED_BY]-(c:Claim)
                            RETURN art";

            var resultUsedAsBacking = await ExecuteQuery(queryUsedAsBacking, new { url });

            if (results != null && results.Count > 0)
            {
                return GetArticleFromResult(results, resultAuthors, resultUsedAsBacking);
            }

            return null;
        }

        private Article? GetArticleFromResult(List<IRecord> results, List<IRecord> authorResults, List<IRecord> backingResults)
        {
            var articleNode = results[0].Values.First().Value;

            var articlePropsJson = JsonConvert.SerializeObject(articleNode.As<INode>().Properties);

            var article = JsonConvert.DeserializeObject<Article>(articlePropsJson);
            
            if(article != null && authorResults!= null && authorResults.Count > 0)
                article.Authors = GetAuthorsFromArticleRelationship(authorResults);

            if(article != null && backingResults != null)
                article.UsedAsBacking = backingResults.Count;

            return article;
        }

        private List<Author> GetAuthorsFromArticleRelationship(List<IRecord> authorResults)
        {
            var authors = new List<Author>();

            foreach (var authorRelation in authorResults)
            {
                var authorNode = authorRelation.Values.First().Value;

                var authorPropsJson = JsonConvert.SerializeObject(authorNode.As<INode>().Properties);

                var author = JsonConvert.DeserializeObject<Author>(authorPropsJson);

                if(author!=null)
                    authors.Add(author);
            }

            return authors;
        }

        public async Task<Author?> GetAuthorByID(string id)
        {
            var query = @"MATCH (aut:Author{id:$id})
                            RETURN aut";

            var results = await ExecuteQuery(query, new { id });

            if (results != null && results.Count > 0)
            {
                return GetAuthorFromResult(results);
            }

            return null;
        }

        private Author? GetAuthorFromResult(List<IRecord> results)
        {
            var authorNode = results[0].Values.First().Value;

            var authorPropsJson = JsonConvert.SerializeObject(authorNode.As<INode>().Properties);

            return JsonConvert.DeserializeObject<Author>(authorPropsJson);
        }
    }
}

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

            if (results != null && results.Count > 0)
            {
                return GetArticleFromResult(results);
            }

            return null;
        }

        private Article? GetArticleFromResult(List<IRecord> results)
        {
            var articleNode = results[0].Values.First().Value;

            var articlePropsJson = JsonConvert.SerializeObject(articleNode.As<INode>().Properties);

            return JsonConvert.DeserializeObject<Article>(articlePropsJson);
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

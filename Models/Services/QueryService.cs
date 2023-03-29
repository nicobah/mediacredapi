using MediaCred.Controllers;
using Neo4j.Driver;
using Newtonsoft.Json;
using System.Text;

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

        public async Task<User?> CreateUser()
        {
            var u = new User();
            u.ID = Guid.NewGuid().ToString();

            var query = GenerateCreateQuery(u, objtype: typeof(User));

            var result = await ExecuteQuery(query);
            return GetUserFromResult(result);
            
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
        private User? GetUserFromResult(List<IRecord> results)
        {
            var userNode = results[0].Values.First().Value;

            var userProps = JsonConvert.SerializeObject(userNode.As<INode>().Properties);

            var user = JsonConvert.DeserializeObject<User>(userProps);

            return user;
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

        private string GenerateCreateQuery(object obj, Type objtype = null, string objID = "o")
        {
            var sb = new StringBuilder();
            try
            {
                if (objtype == null)
                    objtype = obj.GetType();

                sb.Append("CREATE (" + objID + ":" + objtype.Name + " { ");
                sb.Append(GeneratePropertiesString(obj, false, ':', objID));
                sb.Append("})");
            }
            catch (Exception ex) { }

            return sb.ToString();
        }

        private string GeneratePropertiesString(object obj, bool isUpdate, char equalColon, string identifier = "o")
        {
            var sb = new StringBuilder();
            var properties = obj.GetType().GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                if (prop.GetValue(obj) != null && prop.GetValue(obj).ToString().Length > 0)
                {
                    if (i != 0)
                        sb.Append(", ");
                    if (isUpdate)
                        sb.Append(identifier + ".");
                    sb.Append(prop.Name.ToLower() + equalColon + " \"" + prop.GetValue(obj) + "\"");
                }
            }
            return sb.ToString().Trim();
        }
    }
}

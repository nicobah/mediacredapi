using MediaCred.Controllers;
using Neo4j.Driver;
using Neo4jClient;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Xml;

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

            var queryUsedAsBacking = @"MATCH (art:Article{link:$url})<-[:BACKED_BY]-(arg:Argument)
                            RETURN art";

            var resultUsedAsBacking = await ExecuteQuery(queryUsedAsBacking, new { url });

            var queryArguments = @"MATCH (art:Article{link:$url})-[:CLAIMS]->(arg:Argument)
                            RETURN arg";

            var resultArguments = await ExecuteQuery(queryArguments, new { url });

            if (results != null && results.Count > 0)
            {
                return await GetArticleFromResult(results, resultAuthors, resultUsedAsBacking, resultArguments);
            }


            return null;
        }

        public async Task<List<Argument>> GetArgumentsByArticleLink(string url, string? userID)
        {
            var query = @"MATCH (art:Article{link:$url})-[:CLAIMS]->(arg)
                            return arg";

            var results = await ExecuteQuery(query, new { url });

            return await GetArgumentsFromResult(results, userID);
        }

        public async Task<Article?> GetArticleById(string id)
        {
            var query = @"MATCH (art:Article{id:$id})
                            RETURN art";

            var results = await ExecuteQuery(query, new { id });


            if (results != null && results.Count > 0)
            {
                return await GetArticleFromResult(results, null, null, null);
            }

            return null;
        }

        public async Task<Article?> GetArticleByArgumentID(string id)
        {
            var query = @"MATCH (arg:Argument{id:$id})<-[:CLAIMS]-(art:Article)
                            RETURN art";

            var results = await ExecuteQuery(query, new { id });


            if (results != null && results.Count > 0)
            {
                return await GetArticleFromResult(results, null, null, null);
            }

            return null;
        }

        public async Task<bool> IsBackingValid(string ID)
        {

            var query = @"match(arg:Argument{id:$ID}) return arg";
            var results = await ExecuteQuery(query, new { ID });
            var arg = await GetArgumentsFromResult(results);
            if (arg.Count < 1)
            {
                return true;
            }
            else
            {
                return arg.FirstOrDefault().IsValid;
            }

        }

        public async Task<bool> IsAllBackingsValid(string ID)
        {
            var query = @"match (n:Argument{id:$ID})-[:BACKED_BY]->(b)  return b";
            var results = await ExecuteQuery(query, new { ID });
            var arg = await GetArgumentsFromResult(results);
            if (arg.Any(x => !x.IsValid) || arg.Count == 0)
            {
                return false;
            }
            return true;

        }

        public async Task<Article> GetArticleByTopicAndBias(string topic, string bias)
        {
            var query = $"match(art:Article) where art.politicalBias = \"left\" and art.topic = \"Astrology\" return art";
            var results = await ExecuteQuery(query, null);
            return await GetArticleFromResult(results, null, null, null);
        }

        public async Task<User?> GetUserByID(string id)
        {
            var query = @"MATCH (usr:User{id:$id})-[:SUBSCRIBES_TO]->(art:Article)
                            return usr, art";

            var results = await ExecuteQuery(query, new { id });

            return GetUserFromResults(results);
        }

        public async Task<Evidence?> GetEvidenceByID(string id)
        {
            var query = @"MATCH (evd:Evidence{id:$id})
                            return evd";

            var results = await ExecuteQuery(query, new { id });

            return GetEvidenceFromResults(results);
        }

        private Evidence? GetEvidenceFromResults(List<IRecord> results)
        {
            try
            {
                var evidenceNode = results[0].Values.First().Value;

                var evidence = JsonConvert.DeserializeObject<Evidence>(JsonConvert.SerializeObject(evidenceNode.As<INode>().Properties));

                return evidence;
            }
            catch { }

            return null;
        }

        private User? GetUserFromResults(List<IRecord> results)
        {
            try
            {
                var userNode = results[0].Values.First().Value;

                var user = JsonConvert.DeserializeObject<User>(JsonConvert.SerializeObject(userNode.As<INode>().Properties));

                user.SubscribesTo = new List<Article>();

                foreach (var record in results)
                {
                    var articleNode = record.Values.Last().Value;

                    var article = JsonConvert.DeserializeObject<Article>(JsonConvert.SerializeObject(articleNode.As<INode>().Properties));

                    user.SubscribesTo.Add(article);
                }

                return user;
            }
            catch { }

            return null;
        }
        public async Task<List<Argument>> GetRecursiveBackings(string argID, string? userID)
        {
            var query = $"MATCH(a:Argument{{id: \"{argID}\"}})-[b:BACKED_BY*..]->(a2) return a2,a,b";
            var results = await ExecuteQuery(query, null);
            return await GetArgumentsFromResult(results, userID);

        }

        private async Task<Article?> GetArticleFromResult(List<IRecord> results, List<IRecord> authorResults, List<IRecord> backingResults, List<IRecord> arguments)
        {
            var articleNode = results[0].Values.First().Value;

            var articlePropsJson = JsonConvert.SerializeObject(articleNode.As<INode>().Properties);

            var article = JsonConvert.DeserializeObject<Article>(articlePropsJson);

            if (article != null && arguments != null && arguments.Count > 0)
                article.Arguments = await GetArgumentsFromResult(arguments);

            if (article != null && authorResults != null && authorResults.Count > 0)
                article.Authors = GetAuthorsFromArticleRelationship(authorResults);

            if (article != null && backingResults != null)
                article.UsedAsBacking = backingResults.Count;

            return article;
        }

        public async Task<bool> HasEvidence(string argID)
        {
            var query = $"MATCH(arg:Argument{{id: \"{argID}\"}})<-[:PROVES]-(e)" +
                $"RETURN e";

            var results = await ExecuteQuery(query, new { });

            return results.Count > 0;
        }

        public async Task SetArgumentValidity(bool hasEvidence, string argID)
        {
            var query = $"MATCH(arg:Argument{{id: \"{argID}\"}})" +
                $"SET arg.IsValid = {hasEvidence}";

            await ExecuteQuery(query, new { });
        }

        public async Task<List<Argument>> GetBackingsArgument(string argumentID)
        {
            var query = $"MATCH(arg:Argument{{id: \"{argumentID}\"}})-[:BACKED_BY]->(a)" +
                $"RETURN a";

            var results = await ExecuteQuery(query, new { });

            return await GetArgumentsFromResult(results);
        }

        public async Task<List<Argument>> GetArgumentsFromBackingArgument(string backingArgID)
        {
            var query = $"MATCH(arg:Argument{{id: \"{backingArgID}\"}})<-[:BACKED_BY]-(a)" +
                $"RETURN a";

            var results = await ExecuteQuery(query, new { });

            return await GetArgumentsFromResult(results);
        }

        public async Task<List<Argument>> GetArgumentsFromEvidenceID(string evidenceID)
        {
            var query = $"MATCH(e:Evidence{{id: \"{evidenceID}\"}})-[:PROVES]->(arg)" +
                $"RETURN arg";

            var results = await ExecuteQuery(query, new { });

            return await GetArgumentsFromResult(results);
        }

        private async Task<bool> CheckForAccepted(string argID, string userID)
        {
            var query = @"MATCH(usr:User{id:$userID})-[:ACCEPTS]->(arg:Argument{id:$argID})
                            RETURN arg";

            var results = await ExecuteQuery(query, new { argID, userID });

            return results.Any();
        }

        private async Task<List<Argument>> GetArgumentsFromResult(List<IRecord> arguments, string userID = null)
        {
            var argumentsList = new List<Argument>();
            var isAccepted = false;
            string? acceptedID = null;

            foreach (var arg in arguments)
            {
                var argNode = (INode)arg.Values.First().Value;

                var argPropsJson = JsonConvert.SerializeObject(argNode.Properties);
                var argument = JsonConvert.DeserializeObject<Argument>(argPropsJson);
                argument.Neo4JInternalID = argNode.Id;

                argument.ID = argNode.Properties.FirstOrDefault(x => x.Key == "id").Value as string;
                
                if(userID != null && argument != null && argument.ID != null)
                {
                    if(!argument.IsValid)
                    {
                        isAccepted = await CheckForAccepted(argument.ID, userID);
                        argument.IsValid = isAccepted;
                        acceptedID = isAccepted ? argument.ID : null;
                    }
                }

                //relationshiprelated
                try
                {

                var relationShips = (List<Object>)arg.Values.Where(x => x.Key == "b").First().Value;
                foreach (var rel in relationShips)
                {
                    var r = (IRelationship)rel;
                    argument.Relationships.Add(new Relationship() { EndNodeId = r.EndNodeId, StartNodeId = r.StartNodeId, Type = r.Type });
                }
                }catch(Exception e)
                {

                }



                if (argument != null)
                    argumentsList.Add(argument);
            }

            GetBaseArgument(arguments, argumentsList);

            if (isAccepted)
            {
                var originID = acceptedID;
                var invalidArgs = argumentsList.Where(x => x.IsValid == false).ToList();
                var count = invalidArgs.Count;
                for (int i = 0; i < count; i++)
                {
                    var query = $"MATCH(arg:Argument)-[:BACKED_BY]->(argOrigin:Argument{{id: \"{originID}\" }})" +
                        $" RETURN arg";

                    var results = await ExecuteQuery(query);

                    if (results.Any())
                    {
                        var argNode = results.FirstOrDefault().Values.First().Value as INode;
                        var argPropsJson = JsonConvert.SerializeObject(argNode.Properties);
                        var argument = JsonConvert.DeserializeObject<Argument>(argPropsJson);

                        var queryTwo = $"MATCH(arg:Argument)<-[:BACKED_BY]-(argOrigin:Argument{{id: \"{argument.ID}\" }})" +
                        $" WHERE NOT arg.id = \"{originID}\" AND NOT arg.IsValid = false" +
                        $" RETURN arg";

                        var resultsTwo = await ExecuteQuery(queryTwo);

                        if(!resultsTwo.Any())
                        {
                            argument.IsValid = true;
                            var existingArg = argumentsList.Where(x => x.ID == argument.ID).FirstOrDefault();
                            if (existingArg != null)
                            {
                                argumentsList.Remove(existingArg);
                                originID = argument.ID;
                                argument.Relationships = existingArg.Relationships;
                                argument.Neo4JInternalID = existingArg.Neo4JInternalID;
                                argumentsList.Add(argument);
                            }
                        }else
                        {
                            var validCount = 0;
                            foreach(var res in resultsTwo)
                            {
                                var argNodeTwo = res.Values.First().Value as INode;
                                var argPropsJsonTwo = JsonConvert.SerializeObject(argNodeTwo.Properties);
                                var argumentTwo = JsonConvert.DeserializeObject<Argument>(argPropsJsonTwo);

                                if (argumentTwo.IsValid)
                                    validCount++;
                            }
                            if(validCount == resultsTwo.Count) 
                            {
                                argument.IsValid = true;
                                var existingArg = argumentsList.Where(x => x.ID == argument.ID).FirstOrDefault();
                                if (existingArg != null)
                                {
                                    argumentsList.Remove(existingArg);
                                    originID = argument.ID;
                                    argument.Relationships = existingArg.Relationships;
                                    argument.Neo4JInternalID = existingArg.Neo4JInternalID;
                                    argumentsList.Add(argument);
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return argumentsList;
        }

        private static void GetBaseArgument(List<IRecord> arguments, List<Argument> argumentsList)
        {
            try
            {

            var x = arguments.FirstOrDefault()?.Values.Where(x => x.Key == "a").First().Value;
            var y = (INode?)x;
            var argp = JsonConvert.SerializeObject(y.Properties);
            var argument = JsonConvert.DeserializeObject<Argument>(argp);
            argument.Neo4JInternalID = y?.Id;
            argumentsList.Add(argument);
            }catch(Exception e) { }

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

                if (author != null)
                    authors.Add(author);
            }

            return authors;
        }

        public async Task<Dictionary<User, double>> GetSubscribers(Article art)
        {
            var query = @"MATCH (sub:User)-[score:SUBSCRIBES_TO]->(art:Article)
                            RETURN sub, score";

            var results = await ExecuteQuery(query);

            return GetUsersAndScoresFromResult(results);
        }

        public async Task<Dictionary<string, double>> GetSubscribersStringKey(Article art)
        {
            var query = @"MATCH (sub:User)-[score:SUBSCRIBES_TO]->(art:Article)
                            RETURN sub, score";

            var results = await ExecuteQuery(query);

            var dict = GetUsersAndScoresFromResult(results);

            var newDict = new Dictionary<string, double>();

            foreach (var keyval in dict)
            {
                newDict.Add(keyval.Key.Name, keyval.Value);
            }

            return newDict;
        }

        private Dictionary<User, double> GetUsersAndScoresFromResult(List<IRecord> results)
        {
            var dict = new Dictionary<User, double>();

            foreach (var res in results)
            {
                var userNode = res.Values.FirstOrDefault().Value;
                var scoreRelation = res.Values.LastOrDefault().Value;

                if (userNode != null && scoreRelation != null)
                {
                    var user = JsonConvert.DeserializeObject<User>(JsonConvert.SerializeObject(userNode.As<INode>().Properties));
                    var score = JsonConvert.DeserializeObject<SubscribesTo>(JsonConvert.SerializeObject(scoreRelation.As<IRelationship>().Properties));

                    if (user != null && score != null)
                        dict.Add(user, score.Score);
                }
            }

            return dict;
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

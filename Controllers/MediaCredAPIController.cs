using Microsoft.AspNetCore.Mvc;
using System;
using Neo4j.Driver;
using System.Text.Json;
using Newtonsoft.Json;
using System.Reflection;
using MediaCred.Models;
using System.Text;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using MediaCred.Models.ArticleEvaluation;
using MediaCred.Models.Services;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace MediaCred.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MediaCredAPIController : ControllerBase
    {

        private bool _disposed = false;
        private readonly ILogger<MediaCredAPIController> _logger;
        private QueryService qs;
        //public DriverIntroductionExample(string uri, string user, string password)
        //{
        //    _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        //}

        public MediaCredAPIController()
        {
            qs = new QueryService(_disposed,_logger);
        }

        [HttpGet("IsUp")]
        public async Task<string> IsUp()
        {
            //var uri = "neo4j+s://64d3b06c.databases.neo4j.io";
            //var user = "neo4j";
            //var password = "k7by2DDGbQvb98r5geSqJMLf1TRBlL_EWeGHqhrxn8M";
            //var w = new MediaCredAPIController();

            return "true";
        }

        //[HttpGet("GetLinkInfo")]
        //public async Task<Node> GetLinkInfo(string url)
        //{
        //    var query = @"MATCH (art:Article{link: $url})-[r]-(b)
        //                    RETURN art, r, b";

        //    return await ExecuteQuery(query, new { url });
        //}

        [HttpPost("AuthorCredibility")]
        public async Task<string> GetAuthorCredibility(AuthorEvalDto dto)
        {
            try
            {
                var author = await qs.GetAuthorByID(dto.AuthorId);
                //List of evaluation for a param, the weight it has, and the description of the eval
                List<(double, double, string, string)> results = new List<(double, double, string, string)>();
                foreach (var eval in dto.Evals)
                {
                    var currentEval = TranslateEvals(eval.Key);
                    if (currentEval != null)
                    {
                        results.Add((currentEval.GetEvaluation(author).Result, eval.Value, currentEval.Description, currentEval.Name));
                    }
                }

                return JsonConvert.SerializeObject(results);
            }
            catch (Exception ex) { }

            return "failed";

        }

        [HttpPost("ArticleCredibility")]
        public async Task<string> GetArticleCredibility(ArticleEvalDto dto)
        {
            try
            {
                var article = await qs.GetArticleByLink(dto.ArticleLink);

                if (dto.AuthorEvals != null && dto.AuthorEvals.Count > 0 && article.Authors != null && article.Authors.Count > 0)
                {
                    foreach (var author in article.Authors)
                    {
                        var authorCred = 0.0;
                        foreach (var eval in dto.AuthorEvals)
                        {
                            var currentEval = TranslateEvals(eval.Key);
                            if (currentEval != null)
                            {
                                authorCred += currentEval.GetEvaluation(author).Result * (eval.Value/100);
                            }
                        }
                        author.Credibility = authorCred;
                    }
                }

                //List of evaluation for a param, the weight it has, and the description of the eval
                List<(double, double, string, string)> results = new List<(double, double, string, string)>();
                foreach (var eval in dto.ArticleEvals)
                {
                    var currentEval = TranslateEvalsArticle(eval.Key);
                    if (currentEval != null && article != null)
                    {
                        results.Add((currentEval.GetEvaluation(article).Result, eval.Value, currentEval.Description, currentEval.Name));
                    }
                }

                //TO-DO: Store credibility score in histogram DB (see explanation below)

                return JsonConvert.SerializeObject(results);
            }

            catch(Exception ex) { }
            
            return "failed";
        }

        [HttpGet("GetArticleHistogram")]
        public async Task<string> GetArticleHistogram(string id)
        {
            //TO-DO: Create article histogram method to extract histogram data of an articles
            //credibility scores from each user who accessed it, to see how the current user's score
            //compares to other user's score. It should probably be extracted from a simple DB holding
            //doubles, and then put into a dictionary to produce the histogram.
            //Link for inspiration: https://stackoverflow.com/questions/926067/simple-histogram-generation-of-integer-data-in-c-sharp

            //return GetArticleHistogram(id);

            return "not implemented yet";
        }


        [HttpGet("GetLinkToulmin")]
        public async Task<string> GetLinkToulmin(string url)
        {
            var query = @"MATCH (art:Article{link: $url})-[r]->(b:Argument), (b)-[rTwo]->(a:Article)
                            RETURN art, r, b, rTwo, a";

            var results = await qs.ExecuteQuery(query, new { url });

            if (results.Count == 0)
            {
                query = @"MATCH (art:Article{link: $url})-[r]->(b:Argument)
                            RETURN art, r, b";

                results = await qs.ExecuteQuery(query, new { url });
            }


            return JsonConvert.SerializeObject(await GetToulminString(results), Formatting.Indented);
        }

        [HttpGet("AuthorFilterName")]
        public async Task<List<Author>> GetAuthorsByName(string name)
        {
            var query = $@"MATCH (n:Author)
                            WHERE n.name CONTAINS '{name}' 
                            RETURN n";


            var results = await qs.ExecuteQuery(query, null);

            if (results != null && results.Count > 0)
            {
                var authors = new List<Author>();
                foreach (var res in results)
                {
                    authors.Add(GetAuthorFromResult(res));
                }
                return authors;
            }

            return null;

        }

        [HttpPost("CreateArticle")]
        public async Task CreateArticle(Article art)
        {
            var query = $"MATCH(aut:Author{{id: \"{art.AuthorID}\"}}) ";
            query += GenerateCreateQuery(art, objID: "a") + ", (a)-[:WRITTEN_BY]->(aut)";
            
            await qs.ExecuteQuery(query, new { art.Title, art.AuthorID, art.Publisher, art.Link, art.InappropriateWords, art.References, art.Topic });
        }

        [HttpPost("CreateAuthor")]
        public async Task CreateAuthor(AuthorApiDto author)
        {
            author.ID = Guid.NewGuid().ToString();

            var query = GenerateCreateQuery(author, objtype: typeof(Author));

            await qs.ExecuteQuery(query);
        }

        [HttpPost("CreateArgument")]
        public async Task CreateArgument([FromQuery] Article article, [FromQuery] Argument argument)
        {
            var query = $"MATCH(art:Article{{title: \"{article.Title}\", publisher: \"{article.Publisher}\"}}) ";
            query += GenerateCreateQuery(argument, objID: "arg") + ", (art)-[:CLAIMS]->(arg)";

            await qs.ExecuteQuery(query, new { article.Title, article.Publisher, argument.Claim, argument.Ground, argument.Warrant });
        }

        [HttpPost("CreateBacking")]
        public async Task CreateBacking([FromQuery] Article article, [FromQuery] Argument argument)
        {
            var query = $"MATCH(art:Article{{title: \"{article.Title}\", publisher: \"{article.Publisher}\"}}), (arg:Argument{{claim: \"{argument.Claim}\"}}) CREATE (arg)-[:BACKED_BY]->(art)";

            await qs.ExecuteQuery(query, new { article.Title, article.Publisher, argument.Claim });
        }

        //[HttpPost("UpdateArticle")]
        //public async Task<Node> UpdateArticle([FromQuery] Article art, [FromQuery] Article newArticle)
        //{
        //    //TODO: Make the two incoming nodes to a wrapper class, else it wont work.
        //    var query = GenerateUpdateQuery(art, newArticle);

        //    return await ExecuteQuery(query, new { art.Title, art.Publisher, art.Link });
        //}

        

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

        private string GenerateUpdateQuery(object obj, object updateObj)
        {
            var sb = new StringBuilder();
            try
            {
                var identifier = "o";
                sb.Append("MATCH (" + identifier + ":" + obj.GetType().Name + " { ");
                sb.Append(GeneratePropertiesString(obj, false, ':') + "}) ");
                sb.Append("SET " + GeneratePropertiesString(updateObj, true, '=', identifier));
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

        private async Task<string> GetToulminString(List<IRecord> resultRecords)
        {
            try
            {
                if (resultRecords.Any())
                {
                    var backingScore = 0;
                    var backingCount = 0;

                    var rebutScore = 0;
                    var rebuttalCount = 0;

                    foreach (var record in resultRecords)
                    {
                        var artOriginNode = JsonConvert.DeserializeObject<Article>(JsonConvert.SerializeObject(record[0].As<INode>().Properties));
                        var claimedByRelation = JsonConvert.DeserializeObject<Relationship>(JsonConvert.SerializeObject(record[1].As<IRelationship>()));
                        var argumentNode = JsonConvert.DeserializeObject<Argument>(JsonConvert.SerializeObject(record[2].As<INode>().Properties));

                        if (record.Keys.Count <= 3)
                        {
                            if (argumentNode.Warrant != null && argumentNode.Warrant.Length > 1 && argumentNode.Ground != null && argumentNode.Ground.Length > 1)
                            {
                                return "normal fit";
                            }

                            return "weak fit";
                        }

                        if (record.Keys.Count == 5)
                        {
                            var backedOrDisputedRelation = JsonConvert.DeserializeObject<Relationship>(JsonConvert.SerializeObject(record[3].As<IRelationship>()));
                            var backingOrRebutNode = JsonConvert.DeserializeObject<Article>(JsonConvert.SerializeObject(record[4].As<INode>().Properties));

                            if (artOriginNode.Link != backingOrRebutNode.Link)
                            {
                                if (argumentNode.Warrant == null || argumentNode.Warrant.Length < 2 || argumentNode.Ground == null || argumentNode.Ground.Length < 2)
                                    return "weak fit";

                                if (backedOrDisputedRelation.Type.ToLower() == "backed_by")
                                {
                                    backingCount++;
                                    backingScore += backingOrRebutNode.Credibility.HasValue ? backingOrRebutNode.Credibility.Value : 0;
                                }
                                else if (backedOrDisputedRelation.Type.ToLower() == "disputed_by")
                                {
                                    rebuttalCount++;
                                    rebutScore += backingOrRebutNode.Credibility.HasValue ? backingOrRebutNode.Credibility.Value : 0;
                                }
                            }
                        }
                    }
                    if (backingCount > 0 && rebuttalCount == 0)
                        return "good fit";

                    if (rebuttalCount > 0 && backingCount == 0)
                        return "bad fit";

                    if (backingCount == 0 && rebuttalCount == 0)
                        return "normal fit";

                    if (backingCount > 0 && rebuttalCount > 0)
                    {
                        return backingScore > rebutScore ? "good fit" : "bad fit";
                    }
                }
            }
            catch { }

            return "unknown";
        }

        private async Task<List<NodeRelation>> GetNodesFromResult(List<IRecord> writeResults)
        {
            Console.WriteLine("");

            var articles = new List<Article>();
            var authors = new List<Author>();
            var arguments = new List<Argument>();
            var secondArguments = new List<Argument>();
            var relationships = new List<Relationship>();
            var nodeRelations = new List<NodeRelation>();
            foreach (var result in writeResults)
            {
                var relationJSON = JsonConvert.SerializeObject(result[1].As<IRelationship>());
                var nodePropsFirstNode = JsonConvert.SerializeObject(result[0].As<INode>().Properties);
                var nodePropsSecondNode = JsonConvert.SerializeObject(result[2].As<INode>().Properties);

                var firstNodeAuthor = JsonConvert.DeserializeObject<Author>(nodePropsFirstNode);
                var firstNodeArgument = JsonConvert.DeserializeObject<Argument>(nodePropsFirstNode);
                var firstNodeArticle = JsonConvert.DeserializeObject<Article>(nodePropsFirstNode);

                var secondNodeAuthor = JsonConvert.DeserializeObject<Author>(nodePropsSecondNode);
                var secondNodeArgument = JsonConvert.DeserializeObject<Argument>(nodePropsSecondNode);
                var secondNodeArticle = JsonConvert.DeserializeObject<Article>(nodePropsSecondNode);
                var relationship = JsonConvert.DeserializeObject<Relationship>(relationJSON);

                var nodeRelation = new NodeRelation();

                //The node on [0] and [2] can be any of the following types of node: Author, Argument, Article.
                //The object will not be null when deserializing to the wrong type, but the properties will be empty/null, so check for those to correctly deserialize
                var firstNodeStr = "";
                Type firstNodeType = null;
                if (firstNodeAuthor != null && firstNodeAuthor.Name != null && firstNodeAuthor.Name.Length > 1)
                {
                    var indexArticle = authors.FindIndex(x => x.Name.ToLower() == firstNodeAuthor.Name.ToLower());
                    if (indexArticle < 0)
                        authors.Add(firstNodeAuthor);
                    firstNodeStr = firstNodeAuthor.Name;
                    firstNodeType = firstNodeAuthor.GetType();
                    nodeRelation.OriginNode = firstNodeAuthor;
                }

                if (firstNodeArgument != null && firstNodeArgument.Claim != null && firstNodeArgument.Claim.Length > 1)
                {
                    var indexArticle = arguments.FindIndex(x => x.Claim.ToLower() == firstNodeArgument.Claim.ToLower());
                    if (indexArticle < 0)
                        arguments.Add(firstNodeArgument);
                    firstNodeStr = firstNodeArgument.Claim;
                    firstNodeType = firstNodeArgument.GetType();
                    nodeRelation.OriginNode = firstNodeArgument;
                }

                if (firstNodeArticle != null && firstNodeArticle.Title != null && firstNodeArticle.Title.Length > 1)
                {
                    var indexArticle = articles.FindIndex(x => x.Title.ToLower() == firstNodeArticle.Title.ToLower());
                    if (indexArticle < 0)
                        articles.Add(firstNodeArticle);
                    firstNodeStr = firstNodeArticle.Title;
                    firstNodeType = firstNodeArticle.GetType();
                    nodeRelation.OriginNode = firstNodeArticle;
                }


                var secondNode = "";
                Type secondNodeType = null;
                if (secondNodeAuthor != null && secondNodeAuthor.Name != null && secondNodeAuthor.Name.Length > 1)
                {
                    var indexAuthor = authors.FindIndex(x => x.Name.ToLower() == secondNodeAuthor.Name.ToLower());
                    if (indexAuthor < 0)
                        authors.Add(secondNodeAuthor);
                    secondNode = secondNodeAuthor.Name;
                    secondNodeType = secondNodeAuthor.GetType();
                    nodeRelation.FinalNode = secondNodeAuthor;
                }
                else if (secondNodeArgument != null && secondNodeArgument.Claim != null && secondNodeArgument.Claim.Length > 1)
                {
                    var indexArg = arguments.FindIndex(x => x.Claim.ToLower() == secondNodeArgument.Claim.ToLower());
                    if (indexArg < 0)
                    {
                        secondArguments.Add(secondNodeArgument);
                        arguments.Add(secondNodeArgument);
                    }
                    secondNode = secondNodeArgument.Claim;
                    secondNodeType = secondNodeArgument.GetType();
                    nodeRelation.FinalNode = secondNodeArgument;
                }
                else if (secondNodeArticle != null && secondNodeArticle.Title != null && secondNodeArticle.Title.Length > 1)
                {
                    var indexArticle = articles.FindIndex(x => x.Title.ToLower() == secondNodeArticle.Title.ToLower());
                    if (indexArticle < 0)
                        articles.Add(secondNodeArticle);
                    secondNode = secondNodeArticle.Title;
                    secondNodeType = secondNodeArticle.GetType();
                    nodeRelation.FinalNode = secondNodeArticle;
                }

                var relation = "";
                if (relationship != null)
                {
                    var indexRelation = relationships.FindIndex(x => x.Type.ToLower() == relationship.Type.ToLower());
                    if (indexRelation < 0)
                        relationships.Add(relationship);
                    relation = relationship.Type;
                    nodeRelation.RelationshipNode = relationship;
                }

                var relationString = firstNodeType != null && secondNodeType != null
                    ? FormatRelationString(firstNodeType, secondNodeType, relation, firstNodeStr, secondNode)
                    : "Unknown Types.";

                nodeRelation.IsLeftToRight = IsLeftToRight(firstNodeType, secondNodeType, relation);
                nodeRelations.Add(nodeRelation);
                Console.WriteLine(relationString);

            }
            Console.WriteLine("\nUnique Articles: ");
            foreach (var art in articles)
            {
                Console.WriteLine(art.ToString());
            }
            Console.WriteLine("\nUnique Authors: ");
            foreach (var aut in authors)
            {
                Console.WriteLine(aut.ToString());
            }
            Console.WriteLine("\nUnique Arguments: ");
            foreach (var arg in arguments)
            {
                Console.WriteLine(arg.ToString());
            }
            Console.WriteLine("\nUnique Relationships: ");
            foreach (var rel in relationships)
            {
                Console.WriteLine(rel.ToString());
            }

            foreach (var arg in secondArguments)
            {
                var claimDesc = arg.Claim;
                var query = @"
                MATCH (arg:Argument{claim: $claimDesc})-[r]-(b)
                RETURN arg, r, b";
                await qs.ExecuteQuery(query, new { claimDesc });
            }

            Console.WriteLine("Outputting nodeRelations:");
            foreach (var rel in nodeRelations)
            {
                Console.WriteLine(rel.ToString());
            }

            return nodeRelations;
        }

        private string FormatRelationString(Type firstNodeType, Type secondNodeType, string relation, string firstNode, string secondNode)
        {
            var direction = "??";

            if (IsLeftToRight(firstNodeType, secondNodeType, relation))
                direction = " - " + relation + " -> ";
            else
                direction = " <- " + relation + " - ";

            return firstNode + direction + secondNode;
        }

        private bool IsLeftToRight(Type firstNodeType, Type secondNodeType, string relation)
        {
            if ((firstNodeType == typeof(Article) && secondNodeType == typeof(Author))
                || (firstNodeType == typeof(Article) && secondNodeType == typeof(Argument))
                || (firstNodeType == typeof(Argument) && secondNodeType == typeof(Article) && relation.ToLower() == "backed_by"))
                return true;
            else
                return false;
        }

        private IAuthorCredibilityEvaluation? TranslateEvals(string key)
        {
            switch (key)
            {
                case "information":
                    return new AuthorInformationEvaluation();
                default:
                    return null;
            }
        }

        private Author? GetAuthorFromResult(IRecord res)
        {
            var authorNode = res.Values.First().Value;

            var authorPropsJson = JsonConvert.SerializeObject(authorNode.As<INode>().Properties);

            return JsonConvert.DeserializeObject<Author>(authorPropsJson);
        }

        private IArticleCredibilityEvaluation? TranslateEvalsArticle(string key)
        {
            switch (key)
            {
                case "information":
                    return new ArticleInformationEvaluation();
                case "inappropriatewords":
                    return new ArticleIWEvaluation();
                case "references":
                    return new ArticleRefEvaluation();
                case "topic":
                    return new ArticleTopicEvaluation();
                case "author":
                    return new ArticleAuthorEvaluation();
                case "backings":
                    return new ArticleBackingEvaluation();
                default:
                    return null;
            }
        }


    }
}
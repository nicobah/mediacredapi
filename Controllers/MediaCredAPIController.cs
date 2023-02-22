using Microsoft.AspNetCore.Mvc;
using System;
using Neo4j.Driver;
using System.Text.Json;
using Newtonsoft.Json;
using System.Reflection;
using MediaCred.Models;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace MediaCred.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MediaCredAPIController : ControllerBase
    {

        private bool _disposed = false;
        private readonly IDriver _driver;
        //public DriverIntroductionExample(string uri, string user, string password)
        //{
        //    _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        //}

        private readonly ILogger<MediaCredAPIController> _logger;



        public MediaCredAPIController()
        {
            _driver = GraphDatabase.Driver("neo4j+s://64d3b06c.databases.neo4j.io", AuthTokens.Basic("neo4j", "k7by2DDGbQvb98r5geSqJMLf1TRBlL_EWeGHqhrxn8M"));

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

        [HttpGet("GetLinkCredibility")]
        public async Task<string> GetLinkCredibility(string url)
        {
            //TODO: Create credibility calculation and return value
            return "Not implemented yet.";
        }

        [HttpGet("GetLinkToulmin")]
        public async Task<string> GetLinkToulmin(string url)
        {
            var query = @"MATCH (art:Article{link: $url})-[r]->(b:Argument), (b)-[rTwo]->(a:Article)
                            RETURN art, r, b, a, rTwo";

            var results = await ExecuteQuery(query, new { url });

            if(results.Count == 0)
            {
                query = @"MATCH (art:Article{link: $url})-[r]->(b:Argument)
                            RETURN art, r, b";
                
                results = await ExecuteQuery(query, new { url });
            }


            return await GetToulminString(results);
        }

        [HttpPost("CreateArticle")]
        public async Task CreateArticle(Article art)
        {
            var query = GenerateCreateQuery(art);

            await ExecuteQuery(query, new { art.Title, art.Publisher, art.Link});
        }

        [HttpPost("CreateAuthor")]
        public async Task CreateAuthor([FromQuery] Article article, [FromQuery] Author author)
        {
            var query = $"MATCH(art:Article{{title: \"{article.Title}\", publisher: \"{article.Publisher}\"}}) ";
            query += GenerateCreateQuery(author,"a") + ", (art)-[:WRITTEN_BY]->(a)";

            await ExecuteQuery(query, new { article.Title, article.Publisher, author.Name, author.Age });
        }

        [HttpPost("CreateArgument")]
        public async Task CreateArgument([FromQuery] Article article, [FromQuery] Argument argument)
        {
            var query = $"MATCH(art:Article{{title: \"{article.Title}\", publisher: \"{article.Publisher}\"}}) ";
            query += GenerateCreateQuery(argument,"arg") + ", (art)-[:CLAIMS]->(arg)";

            await ExecuteQuery(query, new { article.Title, article.Publisher, argument.Claim, argument.Ground, argument.Warrant });
        }

        [HttpPost("CreateBacking")]
        public async Task CreateBacking([FromQuery] Article article, [FromQuery] Argument argument)
        {
            var query = $"MATCH(art:Article{{title: \"{article.Title}\", publisher: \"{article.Publisher}\"}}), (arg:Argument{{claim: \"{argument.Claim}\"}}) CREATE (arg)-[:BACKED_BY]->(art)";

            await ExecuteQuery(query, new { article.Title, article.Publisher, argument.Claim });
        }

        //[HttpPost("UpdateArticle")]
        //public async Task<Node> UpdateArticle([FromQuery] Article art, [FromQuery] Article newArticle)
        //{
        //    //TODO: Make the two incoming nodes to a wrapper class, else it wont work.
        //    var query = GenerateUpdateQuery(art, newArticle);

        //    return await ExecuteQuery(query, new { art.Title, art.Publisher, art.Link });
        //}

        private async Task<List<NodeRelation>> ExecuteQuery(string query, object parameters)
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
                var results = await GetNodesFromResult(writeResults, parameters);
                //var relation = results.FirstOrDefault(x => x.OriginNode is Article && x.IsLeftToRight && x.FinalNode is Argument);
                //var article = relation != null ? relation.OriginNode as Article : new Article { Title = "not found" };
                return results;
            }
            // Capture any errors along with the query and data for traceability
            catch (Neo4jException ex)
            {
                Console.WriteLine($"{query} - {ex}");
                throw;
            }
        }

        private string GenerateCreateQuery(object obj, string objID = "o")
        {
            var sb = new StringBuilder();
            try
            {
                sb.Append("CREATE (" + objID + ":" + obj.GetType().Name + " { ");
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
                sb.Append("MATCH ("+identifier+":" + obj.GetType().Name + " { ");
                sb.Append(GeneratePropertiesString(obj, false, ':') + "}) ");
                sb.Append("SET " + GeneratePropertiesString(updateObj, true, '=', identifier));
            }
            catch(Exception ex) { }

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
                        sb.Append(identifier+".");
                    sb.Append(prop.Name.ToLower() + equalColon + " \"" + prop.GetValue(obj) + "\"");
                }
            }
            return sb.ToString().Trim();
        }

        private async Task<string> GetToulminString(List<NodeRelation> nodeRelations)
        {
            if(nodeRelations.Count < 2)
            {
                if(nodeRelations.First().FinalNode is Argument)
                {
                    var argNode = (Argument)nodeRelations.First().FinalNode;
                    if (argNode.Warrant != null && argNode.Warrant.Length > 1)
                    {
                        return "normal fit";
                    }
                }

                return "weak fit";
            }

            var artNodeFirst = (Article)nodeRelations.First().OriginNode;
            var argNodeFirst = (Argument)nodeRelations.First().FinalNode;

            if (argNodeFirst.Warrant == null || argNodeFirst.Warrant.Length < 2 || argNodeFirst.Ground == null || argNodeFirst.Ground.Length < 2)
                return "weak fit";

            var backingNodeList = nodeRelations.Where(x => x.RelationshipNode.Type.ToLower() == "backed_by").ToList();
            var rebuttalNodeList = nodeRelations.Where(x => x.RelationshipNode.Type.ToLower() == "disputed_by").ToList();

            if (backingNodeList.Count > 0 && rebuttalNodeList.Count == 0)
                return "good fit";

            if (rebuttalNodeList.Count > 0 && backingNodeList.Count == 0)
                return "bad fit";

            if (argNodeFirst.Warrant != null && argNodeFirst.Warrant.Length > 1 && argNodeFirst.Ground != null && argNodeFirst.Ground.Length > 1 && backingNodeList.Count == 0 && rebuttalNodeList.Count == 0)
                return "normal fit";

            if(backingNodeList.Count > 0 && rebuttalNodeList.Count > 0)
            {
                var backingScore = 0;
                var rebutScore = 0;

                foreach(var backing in backingNodeList)
                {
                    var artBack = (Article)backing.FinalNode;
                    if(artBack.Credibility.HasValue)
                        backingScore += artBack.Credibility.Value;
                }

                foreach (var rebut in rebuttalNodeList)
                {
                    var artRebut = (Article)rebut.FinalNode;
                    if (artRebut.Credibility.HasValue)
                        backingScore += artRebut.Credibility.Value;
                }

                var fit = backingScore > rebutScore ? "good fit" : "bad fit";
                return fit;
            }

            return "unknown";
        }

        private async Task<List<NodeRelation>> GetNodesFromResult(List<IRecord> writeResults, object parameters)
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
                await ExecuteQuery(query, new { claimDesc });
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
    }
}
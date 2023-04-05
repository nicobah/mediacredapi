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
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Neo4jClient.Cypher;

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
            qs = new QueryService(_disposed, _logger);
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
                                authorCred += currentEval.GetEvaluation(author).Result * (eval.Value / 100);
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

            catch (Exception ex) { }

            return "failed";
        }

        [HttpGet("GetSubscribers")]
        public async Task<Dictionary<string, double>> GetSubscribers(string id)
        {
            var art = await qs.GetArticleByLink(id);
            return await qs.GetSubscribersStringKey(art);
        }

        [HttpGet("GetUserByID")]
        public async Task<User> GetUserByID(string id)
        {
            return await qs.GetUserByID(id);
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

        [HttpPost("SendEmail")]
        public async Task SendEmail(string toEmail, string subject, string body)
        {
            var es = new EmailService();
            es.Send(toEmail, subject, body);
        }

        [HttpPost("CreateArticle")]
        public async Task CreateArticle(Article art)
        {
            art.ID = Guid.NewGuid().ToString();

            var query = $"MATCH(aut:Author{{id: \"{art.AuthorID}\"}}) ";
            query += GenerateCreateQuery(art, objID: "a") + ", (a)-[:WRITTEN_BY]->(aut)";

            await qs.ExecuteQuery(query, new { art.ID, art.Title, art.AuthorID, art.Publisher, art.Link, art.InappropriateWords, art.References, art.Topic });
        }

        [HttpPost("CreateAuthor")]
        public async Task CreateAuthor(AuthorApiDto author)
        {
            author.ID = Guid.NewGuid().ToString();

            var query = GenerateCreateQuery(author, objtype: typeof(Author));

            await qs.ExecuteQuery(query);
        }

        [HttpPost("CreateArgument")]
        public async Task CreateArgument(string artID, [FromQuery] ArgumentDto argument)
        {
            argument.ID = Guid.NewGuid().ToString();

            var query = $"MATCH(art:Article{{id: \"{artID}\"}}) ";
            query += GenerateCreateQuery(argument, objtype: typeof(Argument), objID: "arg") + ", (art)-[:CLAIMS]->(arg)";

            await qs.ExecuteQuery(query, new { artID, argument.Claim, argument.Ground, argument.Warrant });
        }

        [HttpPost("CreateBacking")]
        public async Task CreateBacking(string artID, string argID)
        {
            var articleOld = await qs.GetArticleByLink(artID);
            var subscribers = await qs.GetSubscribers(articleOld);

            var queryCreateBacking = $"MATCH(art:Article{{id: $artID}}), (arg:Argument{{claim: \"{argID}\"}}) " +
            $"CREATE (arg)-[:BACKED_BY]->(art)";

            await qs.ExecuteQuery(queryCreateBacking, new { artID, argID });

            CheckForChangesInCredibilityAndNotify(artID, subscribers);
        }

        [HttpPost("AddArticleReadToUser")]
        public async Task<string> AddArticleReadToUser(string articleId, string userId)
        {
           
                var query = GenerateAppendQuery(userId, "articlesRead", "testArticleName", objtype: typeof(User));

                var results = await qs.ExecuteQuery(query, new { userId });

                return query;

            }
   

        [HttpPost("CreateRebuttal")]
        public async Task CreateRebuttal(string artID, string argID, string claimArtID)
        {
            var claimArticleOld = await qs.GetArticleByLink(claimArtID);
            var subscribers = await qs.GetSubscribers(claimArticleOld);

            //toulmin old results
            var resultsOld = await GetToulminResultsForArgument(argID);

            var queryCreateRebuttal = $"MATCH(art:Article{{id: \"{artID}\"}}), (arg:Argument{{claim: \"{argID}\"}}) " +
            $"CREATE (arg)-[:DISPUTED_BY]->(art)";

            var res = await qs.ExecuteQuery(queryCreateRebuttal, new { artID, argID });

            var articleNew = await qs.GetArticleByLink(artID);
            var claimArticleNew = await qs.GetArticleByLink(claimArtID);
            await CheckForChangesInToulmin(resultsOld, claimArticleNew.Arguments.Where(x => x.Claim == argID).FirstOrDefault(), subscribers);
            await CheckForChangesInCredibilityAndNotify(claimArtID, subscribers);
        }

        private async Task<List<IRecord>> GetToulminResultsForArgument(string argID)
        {
            var query = $"MATCH (art:Article)-[r]->(b:Argument{{claim: \"{argID}\"}}), (b)-[rTwo]->(a:Article) RETURN art, r, b, rTwo, a";

            var results = await qs.ExecuteQuery(query, new { argID });

            if (results.Count == 0)
            {
                query = $"MATCH (art:Article)-[r]->(b:Argument{{claim: \"{argID}\"}}) RETURN art, r, b";

                results = await qs.ExecuteQuery(query, new { argID });
            }

            return results;
        }

        private async Task CheckForChangesInToulmin(List<IRecord> oldArgResults, Argument newArg, Dictionary<User, double> subscribers)
        {
            try
            {
                var resultsNew = await GetToulminResultsForArgument(newArg.Claim);

                var toulminStringOld = await GetToulminString(oldArgResults);
                var toulminStringNew = await GetToulminString(resultsNew);

                var oldToulminScore = GetToulminScore(toulminStringOld);
                var newToulminScore = GetToulminScore(toulminStringNew);

                if (newToulminScore < oldToulminScore)
                {
                    foreach (var sub in subscribers)
                    {
                        var es = new EmailService();
                        es.Send("alextholle@gmail.com",
                            "A claim from an article you follow has decreased in credibility!",
                            "Hi " + sub.Key.Name + "! The claim: \"" + newArg.Claim + "\" has fallen in credibility. Please review the claim/article and check if it is not trustworthy anymore.");
                    }
                }
            }
            catch(Exception ex)
            {
                //Missing arguments or something like that error
            }
        }

        private int GetToulminScore(string fit)
        {
            switch (fit)
            {
                case "weak fit":
                    return 1;
                case "normal fit":
                    return 2;
                case "good fit":
                    return 3;
                case "bad fit":
                    return 0;
                default: return 0;
            }
        }

        private async Task CheckForChangesInCredibilityAndNotify(string artID, Dictionary<User, double> oldSubscribersAndScores)
        {
            var articleNew = await qs.GetArticleByLink(artID);
            foreach (var subPair in oldSubscribersAndScores)
            {
                var newScore = GetArticleCredibility(articleNew, subPair.Key);

                if (newScore < subPair.Value - 10)
                {
                    var es = new EmailService();
                    es.Send("alextholle@gmail.com",
                        "An article you follow has decreased in credibility!",
                        "The article " + articleNew.Title + " has fallen in credibility with more than 10 points. Please review it and check if it is not trustworthy anymore.");

                    //TO-DO: Update the SUBSCRIBES_TO relationships with the new scores.
                    var usrID = subPair.Key.ID;
                    var query = $"MATCH(usr:User{{id:\"$usrID\"}})-[score:SUBSCRIBES_TO]->(art:Article{{id:\"$artID\"}})" +
                        $"SET score.score = $newScore";
                    await qs.ExecuteQuery(query, new { usrID, artID, newScore });
                }
            }
        }

        private double GetArticleCredibility(Article article, User user)
        {
            var articleCred = 0.0;
            foreach (var author in article.Authors)
            {
                var authorEval = new AuthorInformationEvaluation();

                author.Credibility = authorEval.GetEvaluation(author).Result * (user.AuthorWeight / 100); //is this correct???
            }

            articleCred += new ArticleInformationEvaluation().GetEvaluation(article).Result * user.InformationWeight;
            articleCred += new ArticleBackingEvaluation().GetEvaluation(article).Result * user.BackingsWeight;
            articleCred += new ArticleAuthorEvaluation().GetEvaluation(article).Result * user.AuthorWeight;
            articleCred += new ArticleIWEvaluation().GetEvaluation(article).Result * user.InappropriateWordsWeight;
            articleCred += new ArticleRefEvaluation().GetEvaluation(article).Result * user.ReferencesWeight;
            articleCred += new ArticleTopicEvaluation().GetEvaluation(article).Result * user.TopicWeight;

            return articleCred;
        }

        [HttpPost("UpdateArticle")]
        public async Task<string> UpdateArticle(string artID, [FromQuery] ArticleDto newArticle)
        {
            var query = GenerateUpdateQuery(artID, newArticle, objtype: typeof(Article));

            var results = await qs.ExecuteQuery(query, new { artID });

            return query;
        }

        [HttpPost("UpdateArgument")]
        public async Task<string> UpdateArgument(string argID, [FromQuery] ArgumentDto updatedArg)
        {
            var query = GenerateUpdateQuery(argID, updatedArg, objtype: typeof(Argument));

            var results = await qs.ExecuteQuery(query, new { argID });

            return query;
        }

        [HttpPost("UpdateAuthor")]
        public async Task<string> UpdateAuthor(string authorID, [FromQuery] AuthorApiDto updatedAuthor)
        {
            var query = GenerateUpdateQuery(authorID, updatedAuthor, objtype: typeof(Author));

            var results = await qs.ExecuteQuery(query, new { authorID });

            return query;
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

        private string GenerateUpdateQuery(string objID, object updateObj, Type objtype = null)
        {
            var sb = new StringBuilder();
            try
            {
                if (objtype == null)
                    objtype = updateObj.GetType();

                var identifier = "o";
                //TO-DO: Link should be changed to ID, and all our nodes should have an ID label.
                sb.Append("MATCH (" + identifier + ":" + objtype.Name + " { id: \"" + objID + "\"}) ");
                //sb.Append(GeneratePropertiesString(obj, false, ':') + "}) ");
                sb.Append("SET " + GeneratePropertiesString(updateObj, true, '=', identifier));
            }
            catch (Exception ex) { }

            return sb.ToString();
        }
        private string GenerateAppendQuery(string objID,  string arrayName, string insertObject, Type objtype = null)
        {
            var sb = new StringBuilder();
            try
            {
                if (objtype == null)
                    objtype = User.GetType();

                var identifier = "o";
                //TO-DO: Link should be changed to ID, and all our nodes should have an ID label.
                sb.Append("MATCH (" + identifier + ":" + objtype.Name + " { id: \"" + objID + "\"}) ");
                //sb.Append(GeneratePropertiesString(obj, false, ':') + "}) ");
                sb.Append($"SET {identifier}.{arrayName}={identifier}.{arrayName}+ '{insertObject}' ");
            }
            catch (Exception ex) { }

            return sb.ToString();
        }


        private string GeneratePropertiesString(object obj, bool isUpdate, char equalColon, string identifier = "o")
        {
            var sb = new StringBuilder();
            var properties = obj.GetType().GetProperties();
            var addedCount = 0;
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                if (prop.GetValue(obj) != null && prop.GetValue(obj).ToString().Length > 0)
                {
                    if (addedCount != 0)
                        sb.Append(", ");
                    if (isUpdate)
                        sb.Append(identifier + ".");
                    sb.Append(prop.Name.ToLower() + equalColon + " \"" + prop.GetValue(obj) + "\"");
                    addedCount++;
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
                    var backingScore = 0.0;
                    var backingCount = 0;

                    var rebutScore = 0.0;
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

        private double? GetEvalTypeAndWeight(Type type, User user)
        {
            if (type == typeof(ArticleInformationEvaluation))
            {
                return user.InformationWeight;
            }
            else if (type == typeof(ArticleIWEvaluation))
            {
                return user.InappropriateWordsWeight;
            }
            else if (type == typeof(ArticleRefEvaluation))
            {
                return user.ReferencesWeight;
            }
            else if (type == typeof(ArticleTopicEvaluation))
            {
                return user.TopicWeight;
            }
            else if (type == typeof(ArticleAuthorEvaluation))
            {
                return user.AuthorWeight;
            }
            else if (type == typeof(ArticleBackingEvaluation))
            {
                return user.BackingsWeight;
            }
            else { return null; }
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
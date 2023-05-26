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
using Xunit.Sdk;
using Microsoft.Extensions.Configuration;
using System.Linq;

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

        [HttpGet("GetArgumentConsistencyScore")]
        public async Task<double> GetArgumentConsistencyScore(string argID)
        {
            return await qs.GetConsistencyWeightForArgument(argID);
        }


        [HttpPost("ArticleCredibility")]
        public async Task<string> GetArticleCredibility(ArticleEvalDto dto)
        {
            try
            {
                var article = await qs.GetArticleByLink(dto.ArticleLink);

                if (dto.AuthorEvals != null && dto.AuthorEvals.Count > 0 && article?.Authors != null && article.Authors.Count > 0)
                {
                    foreach (var author in article.Authors)
                    {
                        var authorCred = 0.0;
                        foreach (var eval in dto.AuthorEvals)
                        {
                            var currentEval = TranslateEvals(eval.Key);
                            if (currentEval != null)
                            {
                                //authorCred += currentEval.GetEvaluation(author).Result * (eval.Value / 100);
                                authorCred += currentEval.GetEvaluation(author).Result;
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

        [HttpGet("ArticleByLink")]
        public async Task<string> GetArticleByLink(string link)
        {
            var art = await qs.GetArticleByLink(link);
            return JsonConvert.SerializeObject(art);
        }

        [HttpPost("AcceptValidity")]
        public async Task AcceptValidity(string argInternalID, string userID)
        {
            var query = $"MATCH (arg:Argument), (usr:User{{ id: \"{userID}\" }})" +
                $" WHERE ID(arg) = {argInternalID}" +
                $" CREATE (usr)-[:ACCEPTS]->(arg)" +
                $" RETURN arg, usr";

            await qs.ExecuteQuery(query, new { argInternalID, userID });
        }

        [HttpGet("ArgTree")]
        public async Task<string> GetArgTree(string argId, string? userID)
        {
            List<Relationship> relationships = new List<Relationship>();
            var args = new List<Argument>();

            var baseArg = await qs.GetArgumentByArgID(argId);

            if (baseArg != null)
                args.Add(baseArg);

            var backingArgs = await qs.GetRecursiveBackings(argId, userID, baseArg);
            backingArgs.AddRange(args.Where(x => !backingArgs.Any(y => y.ID == x.ID)));

            args = backingArgs;

            var evidence = await qs.GetEvidenceRelations(argId);

            relationships.AddRange(args.SelectMany(x => x.Relationships));

            relationships.AddRange(evidence.SelectMany(x => x.Relationships));
            relationships = relationships.GroupBy(x => x.StartNodeId.ToString() + x.EndNodeId.ToString()).Select(y => y.First()).ToList();
            var nodesTemp = args.Select(x => new { id = x.Neo4JInternalID, fill = x.IsValid ? "green" : "red", ll = x.Claim });



            var nodes = nodesTemp.ToList();
            evidence.ForEach(x =>
            {
                nodes.Add(new { id = x.Neo4JInternalID, fill = "purple", ll = x.Name });
            });

            var edges = relationships.Select(x => new { from = x.StartNodeId.ToString(), to = x.EndNodeId.ToString() });

            var data = new
            {
                nodes,
                edges,
                args
            };

            return JsonConvert.SerializeObject(data);
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
        [HttpGet("GetNudge")]
        public async Task<string> GetNudge(string id)
        {
            User? user = await GetUserByID(id);
            //Check if nudge time has been exceeded
            if (user?.NextNudge < DateTime.Now)
            {
                //TODO
                //SET next nudge datetime
                var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
                var h = new Helper(config);
                var nextNudge = h.GetNextNudge(user);
                await qs.SetNudge(user.ID, nextNudge);
                //Get the last article read with a topic
                var topic = await user.GetLatestTopic(qs);

                //Query Database for similar topics and opposite politicalBias
                var article = await qs.GetArticleByTopicAndBias(topic, user.GetOppositeBias());
                //send true, and article
                return JsonConvert.SerializeObject(article);
            }
            else
                return null;

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

        [HttpGet("GetToulminString")]
        public async Task<string> GetToulminString(string argID)
        {
            try
            {
                var queryArg = @"MATCH (arg:Argument{id: $argID})
                            RETURN arg";

                var resultsArg = await qs.ExecuteQuery(queryArg, new { argID });

                var queryBackings = @"MATCH (arg:Argument{id: $argID})-[:BACKED_BY]->(b:Argument)
                            RETURN b";

                var resultsBackings = await qs.ExecuteQuery(queryBackings, new { argID });

                var backingsList = qs.GetArgumentsFromResultsSimple(resultsBackings);

                var queryEvidence = @"MATCH (evd:Evidence)-[:PROVES]->(arg:Argument{id: $argID})
                            RETURN evd";

                var resultsEvidence = await qs.ExecuteQuery(queryEvidence, new { argID });

                var evidenceList = qs.GetEvidenceFromResultsList(resultsEvidence);

                var queryRebuts = @"MATCH (arg:Argument{id: $argID})-[:DISPUTED_BY]->(r:Argument)
                            RETURN r";

                var resultsRebuts = await qs.ExecuteQuery(queryRebuts, new { argID });

                var rebutsList = qs.GetArgumentsFromResultsSimple(resultsRebuts);

                var argumentNode = JsonConvert.DeserializeObject<Argument>(JsonConvert.SerializeObject(resultsArg.FirstOrDefault()[0].As<INode>().Properties));

                return JsonConvert.SerializeObject(GetToulminStringFit(argumentNode, backingsList, rebutsList, evidenceList), Formatting.Indented);
            }
            catch
            {

            }

            return "N/A";
        }

        private string GetToulminStringFit(Argument arg, List<Argument> backings, List<Argument> rebuts, List<Evidence> evidences)
        {
            var backingsCount = backings.Count() + evidences.Count();

            var rebutsCount = rebuts.Count();


            if (arg.Warrant != null && arg.Warrant.Length > 1 && arg.Ground != null && arg.Ground.Length > 1)
            {
                if (backingsCount > 0 && rebutsCount == 0)
                    return "Good";

                if (rebutsCount > 0 && backingsCount == 0)
                    return "Bad";

                if (backingsCount == 0 && rebutsCount == 0)
                    return "Normal";

                if (backingsCount > 0 && rebutsCount > 0)
                {
                    return backingsCount > rebutsCount ? "Good" : "Bad";
                }
            }

            return "Weak";
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

        [HttpPost("CreateEvidence")]
        public async Task CreateEvidence(string artID, Evidence evidence)
        {
            evidence.ID = Guid.NewGuid().ToString();

            var query = $"MATCH(art:Article{{id: \"{artID}\"}}) ";
            query += GenerateCreateQuery(evidence, objtype: typeof(Evidence), objID: "a") + ", (art)-[:PROVES]->(a)";

            await qs.ExecuteQuery(query);
        }

        [HttpDelete("DeleteEvidence")]
        public async Task DeleteEvidence(string evidenceID)
        {
            var arguments = await qs.GetArgumentsFromEvidenceID(evidenceID);

            var query = $"MATCH(e:Evidence{{id: \"{evidenceID}\"}}) DETACH DELETE e";
            await qs.ExecuteQuery(query, new { });

            await UpdateArgumentValidations(arguments);
        }

        [HttpDelete("DeleteArgument")]
        public async Task DeleteArgument(string argID)
        {
            var arguments = await qs.GetArgumentsFromBackingArgument(argID);

            var query = $"MATCH(arg:Argument{{id: \"{argID}\"}}) DETACH DELETE arg";
            await qs.ExecuteQuery(query, new { });

            await UpdateArgumentValidations(arguments);
        }

        private async Task UpdateArgumentValidations(List<Argument> initialArguments)
        {
            for (var i = 0; i < initialArguments.Count; i++)
            {
                var arg = initialArguments[i];

                var hasEvidence = await qs.HasEvidence(arg.ID);

                var allBackingsValid = await qs.IsAllBackingsValid(arg.ID);

                await qs.SetArgumentValidity((hasEvidence || allBackingsValid), arg.ID);

                var argumentsBeingBacked = await qs.GetArgumentsFromBackingArgument(arg.ID);
                if (argumentsBeingBacked.Count > 0)
                {
                    foreach (var a in argumentsBeingBacked)
                    {
                        if (!initialArguments.Where(x => x.ID == a.ID).Any())
                        {
                            initialArguments.Add(a);
                        }
                    }
                }

                /*var backings = await qs.GetBackingsArgument(arg.ID);
                if (backings.Count > 0)
                {
                    foreach (var b in backings)
                    {
                        if (!initialArguments.Where(x => x.ID == b.ID).Any())
                        {
                            initialArguments.Add(b);
                        }
                    }
                }*/

                //if (!updatedIDs.Contains(arg.ID))
                //{
                //updatedIDs.Add(arg.ID);
                //}
            }
        }

        [HttpGet("GetArgsByArtLink")]
        public async Task<List<Argument>> GetArgsByArtLink(string url, string? userID)
        {
            var res = await qs.GetArgumentsByArticleLink(url, userID);
            foreach (var arg in res)
            {
                arg.Weight = await qs.GetConsistencyWeightForArgument(arg.ID);
            }

            return res;
        }

        [HttpGet("GetArtByArgID")]
        public async Task<Article> GetArtByArgID(string argID)
        {
            return await qs.GetArticleByArgumentID(argID);
        }


        [HttpPost("CreateArticle")]
        public async Task CreateArticle(ArticleDto art)
        {
            art.ID = Guid.NewGuid().ToString();

            var query = GenerateCreateQuery(art, objtype: typeof(Article), objID: "a");

            await qs.ExecuteQuery(query, new { art.ID, art.Title, art.Publisher, art.Link, art.InappropriateWords, art.References, art.Topic });
        }
        [HttpPost("AddAuthor")]
        public async Task CreateArticle(AddAuthDto dto)
        {

            var query = $"MATCH(art:Article{{link: \"{dto.link}\"}}), (aut:Author{{id: \"{dto.authorId}\"}}) create (art)-[:WRITTEN_BY]->(aut)";


            await qs.ExecuteQuery(query);
        }

        [HttpPost("CreateAuthor")]
        public async Task CreateAuthor(string? artLink, AuthorApiDto author)
        {
            author.ID = Guid.NewGuid().ToString();

            var query = GenerateCreateQuery(author, objtype: typeof(Author));

            if(artLink != null && artLink.Length > 1)
            {
                query = $"MATCH(art:Article{{link:\"{artLink}\"}}) " + query + ", (art)-[:WRITTEN_BY]->(o)";
            }

            await qs.ExecuteQuery(query);
        }

        [HttpPost("CreateUser")]
        public async Task CreateUser(string name)
        {
            var user = new User() { Name = name, NextNudge = DateTime.Now.AddDays(7) };

            user.ID = Guid.NewGuid().ToString();

            var query = GenerateCreateQuery(user, objtype: typeof(User));

            await qs.ExecuteQuery(query);
        }

        [HttpPost("CreateArgument")]
        public async Task CreateArgument(ArgumentDto argumentDto)
        {
            argumentDto.ID = Guid.NewGuid().ToString();

            var query = $"MATCH(art:Article{{link: \"{argumentDto.artLink}\"}}) ";

            query += $"CREATE(arg:Argument{{id: \"{argumentDto.ID}\", claim: \"{argumentDto.Claim}\", ground: \"{argumentDto.Ground}\", warrant: \"{argumentDto.Warrant}\", isValid: false}})," +
                $"(art)-[:CLAIMS]->(arg)";

            await qs.ExecuteQuery(query, new { argumentDto.ID, argumentDto.Claim, argumentDto.Ground, argumentDto.Warrant });
        }

        [HttpPost("CreateBacking")]
        public async Task CreateBacking(string backedByID, string backedID, string userID, ArticleEvalDto evals)
        {
            var oldArticle = await qs.GetArticleByArgumentID(backedID);
            var subscribers = new List<User>();
            var oldArticleScore = 0.0;

            if (oldArticle != null)
            {
                subscribers = await qs.GetSubscribers(oldArticle);
                var oldArticleScoreString = await GetArticleCredibility(evals);
                if (oldArticleScoreString != null && oldArticleScoreString.Length > 0)
                    oldArticleScore = await GetTotalArticleScore(oldArticleScoreString, evals.ArticleLink, userID);
            }

            var isValidOrEvidence = await qs.IsBackingValid(backedByID);

            var isAllBackingsValid = await qs.IsAllBackingsValid(backedID);

            var queryCreateBacking = $"MATCH(backedBy{{id: $backedByID}}), (backed{{id: \"{backedID}\"}}) " +
            $"CREATE (backed)-[:BACKED_BY]->(backedBy) SET backed.IsValid = {isAllBackingsValid && isValidOrEvidence}";

            await qs.ExecuteQuery(queryCreateBacking, new { backedByID });


            if (oldArticle != null)
            {
                await CheckForChangesInCredibilityAndNotify(oldArticleScore, userID, subscribers, evals);
            }
        }

        private async Task<double> GetTotalArticleScore(string scores, string artLink, string userID)
        {
            var itemList = JsonConvert.DeserializeObject<List<Scores>>(scores);

            var totalScore = 0.0;
            var totalWeight = 0.0;

            for (var i = 0; i < itemList.Count(); i++)
            {
                var score = itemList[i].Item1;
                var weight = itemList[i].Item2;

                totalScore += score * weight;
                totalWeight += weight;
            }

            var sourceScore = totalScore / totalWeight;

            var arguments = await GetArgsByArtLink(artLink, userID);
            var contentScore = 0.0;
            var u = 100 / arguments.Count();
            foreach(var arg in arguments)
            {
                var weight = arg.Weight.HasValue ? arg.Weight.Value : 1;
                contentScore += u * weight;
            }

            var finalScore = (sourceScore * 0.5) + (contentScore * 0.5);

            return finalScore;
        }

        private Dictionary<string, double> GetScoresFromScoreJSON(string scores)
        {
            var itemList = JsonConvert.DeserializeObject <List<Scores>>(scores);
            var dictionary = new Dictionary<string, double>();

            foreach (var item in itemList)
            {
                if (!dictionary.ContainsKey(item.Item4))
                {
                    dictionary[item.Item4] = item.Item1;
                }
            }

            return dictionary;
        }

        private Dictionary<string, Dictionary<double, double>> CombineScoresInDict(Dictionary<string, double> oldScores, Dictionary<string, double> newScores)
        {
            var dict = new Dictionary<string, Dictionary<double, double>>();

            foreach(var score in oldScores)
            {
                try
                {
                    dict[score.Key] = new Dictionary<double, double>();
                    dict[score.Key][score.Value] = newScores[newScores.Keys.Where(x => x == score.Key).FirstOrDefault()];
                }
                catch { }
            }

            return dict;
        }

        [HttpPost("AddArticleReadToUser")]
        public async Task<string> AddArticleReadToUser(string articleId, string userId)
        {

            var query = GenerateAppendQuery(userId, "articlesRead", "testArticleName", objtype: typeof(User));

            var results = await qs.ExecuteQuery(query, new { userId });

            return query;

        }


        [HttpPost("CreateRebuttal")]
        public async Task CreateRebuttal(string disputedByID, string disputedID, string userID, ArticleEvalDto evals)
        {
            var oldArticle = await qs.GetArticleByArgumentID(disputedID);
            var subscribers = new List<User>();
            var oldArticleScore = 0.0;

            if (oldArticle != null)
            {
                subscribers = await qs.GetSubscribers(oldArticle);
                var oldArticleScoreString = await GetArticleCredibility(evals);
                if (oldArticleScoreString != null && oldArticleScoreString.Length > 0)
                    oldArticleScore = await GetTotalArticleScore(oldArticleScoreString, evals.ArticleLink, userID);
            }

            var isValidOrEvidence = await qs.IsBackingValid(disputedByID);

            var isAllBackingsValid = await qs.IsAllBackingsValid(disputedID);

            var queryCreateDispute = $"MATCH(disputedBy{{id: \"{disputedByID}\"}}), (disputed{{id: \"{disputedID}\"}})" +
            $"CREATE (disputed)-[:DISPUTED_BY]->(disputedBy), (evd:Evidence{{id:\"artID{oldArticle.ID}evdTest\"}}), (evd)-[:PROVES]->(disputedBy) SET disputed.IsValid = {isAllBackingsValid && isValidOrEvidence}";

            await qs.ExecuteQuery(queryCreateDispute);


            if (oldArticle != null)
            {
                await CheckForChangesInCredibilityAndNotify(oldArticleScore, userID, subscribers, evals);
            }
        }

        [HttpPost("AddArticleRead")]
        public async Task AddArticleRead(string userID, string url)
        {
            var queryArticle = @"MATCH(art:Article{link:$url}) RETURN art";

            var resultsArticle = await qs.ExecuteQuery(queryArticle, new {url});

            var queryUser = @"MATCH(usr:User{id:$userID}) RETURN usr";

            var resultsUser = await qs.ExecuteQuery(queryUser, new { userID });

            if (resultsArticle.Count > 0 && resultsUser.Count > 0)
            {
                try
                {
                    var artNode = resultsArticle[0].Values.First().Value;
                    var artPropsJson = JsonConvert.SerializeObject(artNode.As<INode>().Properties);
                    var article = JsonConvert.DeserializeObject<Article>(artPropsJson);

                    if (article != null)
                    {
                        var queryAddArticleID = $"MATCH(usr:User{{id: \"{userID}\"}}) WHERE NOT \"{article.ID}\" IN usr.articlesRead SET usr.articlesRead = usr.articlesRead + \"{article.ID}\"";
                        await qs.ExecuteQuery(queryAddArticleID);
                    }
                } catch { }
            }
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

                var toulminStringOld = await GetToulminString(newArg.ID);
                var toulminStringNew = await GetToulminString(newArg.ID);

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
            catch (Exception ex)
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

        private async Task CheckForChangesInCredibilityAndNotify(double oldArticleScore, string userID, List<User> subscribers, ArticleEvalDto evals)
        {
            var articleNew = await qs.GetArticleByLink(evals.ArticleLink);
            var newScoreString = await GetArticleCredibility(evals);
            if (newScoreString != null && newScoreString.Length > 0)
            {
                var newScore = await GetTotalArticleScore(newScoreString, evals.ArticleLink, userID);

                if (newScore < oldArticleScore - 10)
                {
                    foreach (var sub in subscribers)
                    {
                        //User should have an email and then it should send to that
                        var es = new EmailService();
                        es.Send("alextholle@gmail.com",
                            "An article you follow has decreased in credibility!",
                            "The article " + articleNew.Title + " has fallen in credibility with more than 10 points. Please review it and check if it is not trustworthy anymore.");
                    }
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
        private string GenerateAppendQuery(string objID, string arrayName, string insertObject, Type objtype = null)
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
                default:
                    return null;
            }
        }


    }
}
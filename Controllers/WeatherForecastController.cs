using Microsoft.AspNetCore.Mvc;
using System;
using Neo4j.Driver;
using System.Text.Json;
using Newtonsoft.Json;
using System.Reflection;

namespace MediaCred.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {

        private bool _disposed = false;
        private readonly IDriver _driver;
        //public DriverIntroductionExample(string uri, string user, string password)
        //{
        //    _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        //}

        private readonly ILogger<WeatherForecastController> _logger;



        public WeatherForecastController()
        {
            _driver = GraphDatabase.Driver("neo4j+s://64d3b06c.databases.neo4j.io", AuthTokens.Basic("neo4j", "k7by2DDGbQvb98r5geSqJMLf1TRBlL_EWeGHqhrxn8M"));

        }

        [HttpGet(Name = "GetWeatherForecast")]
        public async Task Get()
        {

            var uri = "neo4j+s://64d3b06c.databases.neo4j.io";

            var user = "neo4j";
            var password = "k7by2DDGbQvb98r5geSqJMLf1TRBlL_EWeGHqhrxn8M";
            var w = new WeatherForecastController();
            /*await w.CreateArticle("TestArt-A", artPublisher: "TestPub-A");
            await w.CreateArticle("TestArt-GroundFact1", artGroundFact: "GroundFact-A", artPublisher: "TestPub-GroundFact1");
            await w.CreateAuthor("TestArt-A", "TestPub-A", "Author-A");
            await w.CreateAuthor("TestArt-GroundFact1", "TestPub-GroundFact1", "Author-GroundFact1");
            await w.CreateArgument("TestArt-A", "TestPub-A", "TestClaim-A");
            await w.CreateBacking("TestArt-GroundFact1", "TestPub-GroundFact1", "TestClaim-A");*/
        }

        [HttpGet("GetLinkInfo")]
        public async Task GetLinkInfo(string url)
        {
            var query = @"MATCH (art:Article{link: $url})-[r]-(b)
                            RETURN art, r, b";

            await ExecuteQuery(query, new { url });
        }

        [HttpPost("CreateArticle")]
        public async Task CreateArticle(string artTitle, string? artGroundFact = null, string? artPublisher = null, string? artLink = null, string? artCreatedDate = null, string? artConclusion = null)
        {
            var query = @"
                CREATE (art:Article { 
                    title: $artTitle, 
                    groundFact: $artGroundFact, 
                    publisher: $artPublisher, 
                    link: $artLink, 
                    createdDate: $artCreatedDate, 
                    conclusion: $artConclusion
                    })";

            await ExecuteQuery(query, new { artTitle, artGroundFact, artPublisher, artLink, artCreatedDate, artConclusion });
        }

        [HttpPost("CreateAuthor")]
        public async Task CreateAuthor(string artTitle, string artPublisher, string authorName, string? age = null, string? company = null, string? physCompAddress = null, string? education = null, string? politicalOrientation = null)
        {
            var query = @"
                MATCH(art:Article{title: $artTitle, publisher: $artPublisher})
                CREATE (a:Author { 
                    name: $authorName, 
                    age: $age, 
                    company: $company, 
                    physicalCompanyAddress: $physCompAddress, 
                    education: $education, 
                    politicalOrientation: $politicalOrientation
                    }),
                (art)-[:WRITTEN_BY]->(a)";

            await ExecuteQuery(query, new { artTitle, artPublisher, authorName, age, company, physCompAddress, education, politicalOrientation });
        }

        [HttpPost("CreateArgument")]
        public async Task CreateArgument(string artTitle, string artPublisher, string claim, string? ground = null, string? warrant = null)
        {
            //To match with rel: MATCH(art:Article{title: $artTitle})-[:WRITTEN_BY]->(aut:Author{name: $authorName})
            var query = @"
                MATCH(art:Article{title: $artTitle, publisher: $artPublisher})
                CREATE (arg:Argument { 
                    claim: $claim, 
                    ground: $ground, 
                    warrant: $warrant
                    }),
                (art)-[:CLAIMS]->(arg)";

            await ExecuteQuery(query, new { artTitle, artPublisher, claim, ground, warrant });
        }

        [HttpPost("CreateBacking")]
        public async Task CreateBacking(string artTitle, string artPublisher, string argClaim)
        {
            var query = @"
                MATCH(art:Article{title: $artTitle, publisher: $artPublisher}),
                (arg:Argument{claim: $argClaim})
                CREATE (arg)-[:BACKED_BY]->(art)";

            await ExecuteQuery(query, new { artTitle, artPublisher, argClaim });
        }

        private async Task ExecuteQuery(string query, object parameters)
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
                GetNodesFromResult(writeResults, parameters);
            }
            // Capture any errors along with the query and data for traceability
            catch (Neo4jException ex)
            {
                Console.WriteLine($"{query} - {ex}");
                throw;
            }
        }

        private async void GetNodesFromResult(List<IRecord> writeResults, object parameters)
        {
            Console.WriteLine("");

            var articles = new List<Article>();
            var authors = new List<Author>();
            var arguments = new List<Argument>();
            var secondArguments = new List<Argument>();
            var relationships = new List<Relationship>();
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

                //The node on [0] and [2] can be any of the following types of node: Author, Argument, Article.
                //The object will not be null when deserializing to the wrong type, but the properties will be empty/null, so check for those to correctly deserialize
                var firstNode = "";
                Type firstNodeType = null;
                if (firstNodeAuthor != null && firstNodeAuthor.Name != null && firstNodeAuthor.Name.Length > 1)
                {
                    var indexArticle = authors.FindIndex(x => x.Name.ToLower() == firstNodeAuthor.Name.ToLower());
                    if (indexArticle < 0)
                        authors.Add(firstNodeAuthor);
                    firstNode = firstNodeAuthor.Name;
                    firstNodeType = firstNodeAuthor.GetType();
                }

                if (firstNodeArgument != null && firstNodeArgument.Claim != null && firstNodeArgument.Claim.Length > 1)
                {
                    var indexArticle = arguments.FindIndex(x => x.Claim.ToLower() == firstNodeArgument.Claim.ToLower());
                    if (indexArticle < 0)
                        arguments.Add(firstNodeArgument);
                    firstNode = firstNodeArgument.Claim;
                    firstNodeType = firstNodeArgument.GetType();
                }

                if (firstNodeArticle != null && firstNodeArticle.Title != null && firstNodeArticle.Title.Length > 1)
                {
                    var indexArticle = articles.FindIndex(x => x.Title.ToLower() == firstNodeArticle.Title.ToLower());
                    if (indexArticle < 0)
                        articles.Add(firstNodeArticle);
                    firstNode = firstNodeArticle.Title;
                    firstNodeType = firstNodeArticle.GetType();
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
                }
                else if (secondNodeArticle != null && secondNodeArticle.Title != null && secondNodeArticle.Title.Length > 1)
                {
                    var indexArticle = articles.FindIndex(x => x.Title.ToLower() == secondNodeArticle.Title.ToLower());
                    if (indexArticle < 0)
                        articles.Add(secondNodeArticle);
                    secondNode = secondNodeArticle.Title;
                    secondNodeType = secondNodeArticle.GetType();
                }

                var relation = "";
                if (relationship != null)
                {
                    var indexRelation = relationships.FindIndex(x => x.Type.ToLower() == relationship.Type.ToLower());
                    if (indexRelation < 0)
                        relationships.Add(relationship);
                    relation = relationship.Type;
                }

                var relationString = firstNodeType != null && secondNodeType != null
                    ? FormatRelationString(firstNodeType, secondNodeType, relation, firstNode, secondNode)
                    : "Unknown Types.";

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
        }

        private string FormatRelationString(Type firstNodeType, Type secondNodeType, string relation, string firstNode, string secondNode)
        {
            var direction = "??";

            if ((firstNodeType == typeof(Article) && secondNodeType == typeof(Author))
                || (firstNodeType == typeof(Article) && secondNodeType == typeof(Argument))
                || (firstNodeType == typeof(Argument) && secondNodeType == typeof(Article) && relation.ToLower() == "backed_by"))
                direction = " - " + relation + " -> ";
            else
                direction = " <- " + relation + " - ";

            return firstNode + direction + secondNode;
        }
    }
}
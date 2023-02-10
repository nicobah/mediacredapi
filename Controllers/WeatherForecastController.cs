using Microsoft.AspNetCore.Mvc;
using System;
using Neo4j.Driver;

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

        [HttpGet("IsUp")]
        public bool IsUp()
        {
            return true;
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

                foreach (var result in writeResults)
                {
                    Console.WriteLine("Successful result: " + result.ToString());
                }
            }
            // Capture any errors along with the query and data for traceability
            catch (Neo4jException ex)
            {
                Console.WriteLine($"{query} - {ex}");
                throw;
            }
        }
    }
}
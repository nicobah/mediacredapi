﻿using MediaCred.Controllers;
using Neo4j.Driver;
using Newtonsoft.Json;
using System;

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

        public async Task<User?> GetUserByID(string id)
        {
            var query = @"MATCH (usr:User{ID:$id})
                            return usr"
            ;

            var results = await ExecuteQuery(query, new { id });

            return GetUserFromResults(results);
        }

        private User? GetUserFromResults(List<IRecord> results)
        {
            try
            {
                var userNode = results[0].Values.First().Value;

                var user = JsonConvert.DeserializeObject<User>(JsonConvert.SerializeObject(userNode.As<INode>().Properties));

                return user;
            }
            catch { }

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

        public async Task<Dictionary<User,double>> GetSubscribers(Article art)
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

            foreach(var keyval in dict)
            {
                newDict.Add(keyval.Key.Name, keyval.Value);
            }

            return newDict;
        }

        private Dictionary<User,double> GetUsersAndScoresFromResult(List<IRecord> results)
        {
            var dict = new Dictionary<User,double>();

            foreach(var res in results)
            {
                var userNode = res.Values.FirstOrDefault().Value;
                var scoreRelation = res.Values.LastOrDefault().Value;

                if(userNode !=null && scoreRelation != null)
                {
                    var user = JsonConvert.DeserializeObject<User>(JsonConvert.SerializeObject(userNode.As<INode>().Properties));
                    var score = JsonConvert.DeserializeObject<SubscribesTo>(JsonConvert.SerializeObject(scoreRelation.As<IRelationship>().Properties));

                    if(user != null && score != null)
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
    }
}

using Neo4j.Driver;

namespace MediaCred
{
    public class Relationship
    {

        public string? Type { get; set; }

        public long? StartNodeId { get; set; }

        public long? EndNodeId {get; set;}


   

        public string GetFullString()
        {
            return "Type: " + this.Type;
        }

        public override string ToString()
        {
            return "[:" + this.Type + "]";
        }
    }
}

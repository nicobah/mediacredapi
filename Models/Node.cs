using Neo4j.Driver;

namespace MediaCred.Models
{
    public class Node
    {
        public virtual string GetFullString() { return string.Empty; }
        public string ID { get; set; }
        public long? Neo4JInternalID { get; set; }
        public  List<Relationship?> Relationships { get; set; } = new List<Relationship?>();
    }
}

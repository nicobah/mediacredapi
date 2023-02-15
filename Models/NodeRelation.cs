using Neo4j.Driver;

namespace MediaCred.Models
{
    public class NodeRelation
    {
        public NodeRelation() { }

        public NodeRelation(Node origin, Relationship relation, Node final) 
        { 
            this.OriginNode = origin; 
            this.RelationshipNode = relation;
            this.FinalNode = final;
        }

        public Node OriginNode { get; set; }

        public Relationship RelationshipNode { get; set; }

        public Node FinalNode { get; set; }

        public bool IsLeftToRight { get; set; }

        public override string ToString()
        {
            return IsLeftToRight 
                ? this.OriginNode.ToString() + " - " + this.RelationshipNode + " -> " + this.FinalNode 
                : this.OriginNode.ToString() + " <- " + this.RelationshipNode + " - " + this.FinalNode;
        }
    }
}

namespace MediaCred
{
    public class Relationship
    {   
        public string Type { get; set; }

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

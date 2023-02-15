namespace MediaCred
{
    public class Relationship
    {   
        public string Type { get; set; }

        public override string ToString()
        {
            return "Type: " + this.Type;
        }
    }
}

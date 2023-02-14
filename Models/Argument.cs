namespace MediaCred
{
    public class Argument
    {
        public string Claim { get; set; }

        public string? Ground { get; set; }

        public string? Warrant { get; set; }

        public override string ToString()
        {
            return "claim: " + this.Claim + " ground: " + this.Ground + " warrant: " + this.Warrant;
        }
    }
}

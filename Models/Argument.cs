using MediaCred.Models;
using Microsoft.AspNetCore.SignalR;

namespace MediaCred
{
    public class Argument : Node
    {
        public string ID { get; set; }

        public string Claim { get; set; }

        public string? Ground { get; set; }

        public string? Warrant { get; set; }

        public override string GetFullString()
        {
            return "claim: " + this.Claim + " ground: " + this.Ground + " warrant: " + this.Warrant;
        }

        public override string ToString()
        {
            return this.Claim;
        }
    }
}

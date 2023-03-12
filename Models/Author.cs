using MediaCred.Models;

namespace MediaCred
{
    public class Author : Node
    {
        public string Name { get; set; }
        public int? Age { get; set; }
        public string? Image { get; set; }
        public string? Bio { get; set; }
        public string? Company { get; set;  }
        public string? Education { get; set; }
        public string? PoliticalOrientation { get; set; }
        public override string GetFullString()
        {
            return "Name: " + this.Name + " Age: " + this.Age;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
}

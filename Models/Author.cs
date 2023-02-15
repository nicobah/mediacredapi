using MediaCred.Models;

namespace MediaCred
{
    public class Author : Node
    {
        public string Name { get; set; }

        public string? Age { get; set; }

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

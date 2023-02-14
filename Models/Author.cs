namespace MediaCred
{
    public class Author
    {
        public string Name { get; set; }

        public string? Age { get; set; }

        public override string ToString()
        {
            return "Name: " + this.Name + " Age: " + this.Age;
        }
    }
}

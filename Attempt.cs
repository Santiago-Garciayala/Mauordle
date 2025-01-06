
namespace Mauordle
{
    public class Attempt
    {
        public bool Success { get; set; } = false;
        public string Word { get; set; }
        public DateTime TimeFinished { get; set; }
        public int Guesses { get; set; }

    }
}

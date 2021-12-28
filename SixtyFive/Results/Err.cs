using Disqord;

namespace SixtyFive.Results
{
    public class Err : Result<Err>
    {
        public override bool IsSuccessful => false;
        
        public Err(string s) : base(s) { }
        public Err(LocalEmbed b) : base(b) { }
        public Err(LocalMessage msg) : base(msg) { }

        public Err() {}
        
    }
}

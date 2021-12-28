using Disqord;

namespace SixtyFive.Results
{
    public class Ok : Result<Ok>
    {
        public static Ok Success { get; } = new Ok();

        public override bool IsSuccessful => true;
        
        public Ok(string s) : base(s) { }
        public Ok(LocalEmbed b) : base(b) { }
        public Ok(LocalMessage msg) : base(msg) { }

        public Ok() {}
    }
}
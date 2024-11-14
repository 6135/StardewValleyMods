using StardewModdingAPI;

namespace CoreUtils
{
    internal class Config
    {
        public SButton debugKey { get; set; }

        public Config()
        {
            debugKey = SButton.J;
        }
    }
}

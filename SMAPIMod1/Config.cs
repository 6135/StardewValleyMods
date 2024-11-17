using StardewModdingAPI;

namespace SMAPIMod1
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

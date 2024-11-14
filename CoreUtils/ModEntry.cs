using CoreUtils.management.memory;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Threading;

namespace CoreUtils
{
    public class ModEntry : Mod
    {
        internal Config config;
        internal ITranslationHelper i18n => Helper.Translation;
        internal static string UniqueID;

        public override void Entry(IModHelper helper)
        {
            UniqueID = ModManifest.UniqueID;
            Container.Instance.RegisterInstance(helper, ModManifest.UniqueID);
            Container.Instance.RegisterInstance(Monitor, ModManifest.UniqueID);
        }

        private void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
        }
    }
}
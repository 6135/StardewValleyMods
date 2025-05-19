using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIFramework.Menus
{
    public class DialogMenu : BaseMenu
    {
        public string Message { get; set; }
        public DialogType Type { get; set; } = DialogType.Info;
        public List<DialogButton> Buttons { get; protected set; } = new List<DialogButton>();

        public enum DialogType
        { Info, Warning, Error, Question, Success }

        public class DialogButton
        {
            public string Text { get; set; }
            public Action OnClick { get; set; }
            public bool IsDefault { get; set; }
            public bool IsCancel { get; set; }
        }

        public override void draw(SpriteBatch b);

        public void AddButton(string text, Action onClick, bool isDefault = false, bool isCancel = false);

        public void SetMessage(string message);

        // Static factory methods for common dialogs
        public static DialogMenu CreateInfoDialog(string message, Action onOk = null);

        public static DialogMenu CreateErrorDialog(string message, Action onOk = null);

        public static DialogMenu CreateConfirmDialog(string message, Action onConfirm = null, Action onCancel = null);
    }
}
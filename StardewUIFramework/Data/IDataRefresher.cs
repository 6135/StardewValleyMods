using UIFramework.Core;

namespace UIFramework.Data
{
    /// <summary>Something re-applied by a data menu's refresh hook (<see cref="UIMenu.DataRefresh"/>).</summary>
    internal interface IDataRefresher
    {
        /// <summary>Re-apply; <paramref name="opening"/> is true once per open (and after an in-place rebuild of an open menu).</summary>
        void Refresh(bool opening);
    }
}

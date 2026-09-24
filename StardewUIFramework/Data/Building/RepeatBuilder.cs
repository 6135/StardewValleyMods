using System;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// <c>Repeat</c>: a stack (<c>Horizontal</c>, <c>Spacing</c>, <c>Alignment</c>) holding a non-virtualized copy of its
    /// <c>Children</c> for every row of its source. Inside, the row is <c>row</c> and the <c>As</c> name
    /// (<c>"As": "fruit"</c> → <c>fruit.name</c>); a nested Repeat's rows shadow <c>row</c> but outer rows stay readable by
    /// their names. Copies are rebuilt when the rows change; ids are <c>&lt;repeat id&gt;.&lt;n&gt;.&lt;child id&gt;</c>.
    /// Use <c>List</c> / <c>DataGrid</c> for long collections.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        private void BuildRepeat(BuildContext ctx, UIContainer stack, ElementDefinition def, DataScope scope, DataPath path)
        {
            SourceBinding? source = SourceBinding.Create(def.Repeat, scope, def.As, ctx.Runtime, path.Field("Repeat"), ctx.Log);
            if (source == null)
            {
                return; // reported
            }

            var repeat = new RepeatCollection(this, ctx, stack, def, source, path.Field("Children"));
            repeat.Refresh(opening: true);
            ctx.Runtime.Collections[stack.Id] = repeat;
            ctx.Applier.AddRefresher(repeat);
        }

        /// <summary>Rebuilds a Repeat's copies when its rows change, and refreshes their live values otherwise.</summary>
        private sealed class RepeatCollection : IDataRefresher, IDataCollection
        {
            private readonly DataBuilder builder;
            private readonly BuildContext ctx;
            private readonly UIContainer stack;
            private readonly ElementDefinition def;
            private readonly SourceBinding source;
            private readonly DataPath path;
            private readonly RefresherGroup group;
            private bool built;

            internal RepeatCollection(DataBuilder builder, BuildContext ctx, UIContainer stack, ElementDefinition def, SourceBinding source, DataPath path)
            {
                this.builder = builder;
                this.ctx = ctx;
                this.stack = stack;
                this.def = def;
                this.source = source;
                this.path = path;
                group = new RefresherGroup(ctx.Runtime.Owner, stack.Id);
            }

            public UIElement Element => stack;

            public void Refresh(bool opening)
            {
                if (source.Update(opening) || !built)
                {
                    Build();
                    return;
                }

                group.Refresh(opening);
            }

            public void Rebuild()
            {
                source.Invalidate();
                source.Update(opening: true);
                Build();
            }

            private void Build()
            {
                built = true;
                group.Clear();
                stack.Clear();
                if (def.Children == null)
                {
                    return;
                }

                int count = source.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        builder.BuildChildren(ctx.ForTemplate(group, stack.Id + "." + RowScope.Text(i)), stack, def.Children, source.ScopeFor(i), path);
                    }
                    catch (Exception ex)
                    {
                        UIServices.Log($"[{ctx.Runtime.Owner}] {path}: row {i} could not be built: {ex.Message}", LogLevel.Error);
                    }
                }

                stack.InvalidateLayout();
            }
        }
    }
}

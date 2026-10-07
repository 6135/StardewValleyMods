using System.Text;

using GingerIslandMainlandAdjustments.Framework;

using Microsoft.Xna.Framework;

namespace GingerIslandMainlandAdjustments.ScheduleManager.DataModels;

/// <summary>
/// A single point of a generated schedule. Adapted from atravita's AtraShared (MIT).
/// </summary>
internal sealed class SchedulePoint(
    Random random,
    NPC npc,
    string map,
    int time,
    Point point,
    bool isarrivaltime = false,
    int direction = Game1.down,
    string? animation = null,
    string? basekey = null,
    string? varKey = null)
{
    private readonly string? dialoguekey = npc.GetRandomDialogue(varKey, random) ?? npc.GetRandomDialogue(basekey, random);

    internal bool IsArrivalTime { get; set; } = isarrivaltime;

    internal Point Point { get; } = point;

    internal string? Animation { get; } = animation;

    internal StringBuilder AppendToStringBuilder(StringBuilder sb)
        => sb.AppendSchedulePoint(npc, map, time, this.Point, this.IsArrivalTime, direction, this.Animation, this.dialoguekey);
}

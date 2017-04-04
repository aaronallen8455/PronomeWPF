namespace Pronome.Editor
{
    public class RemoveRepeatGroup : AbstractBeatCodeAction
    {
        protected RepeatGroup Group;

        public RemoveRepeatGroup(RepeatGroup group) : base(group.Row, "Remove Repeat Group")
        {
            Group = group;
        }

        protected override void Transformation()
        {
            Group.Cells.First.Value.RepeatGroups.Remove(Group);
            Group.Cells.Last.Value.RepeatGroups.Remove(Group);
        }
    }
}

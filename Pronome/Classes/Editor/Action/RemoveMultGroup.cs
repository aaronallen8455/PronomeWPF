namespace Pronome.Editor
{
    public class RemoveMultGroup : AbstractBeatCodeAction
    {
        protected MultGroup Group;

        public RemoveMultGroup(MultGroup group) : base(group.Row, "Remove Multiply Group")
        {
            Group = group;
        }

        protected override void Transformation()
        {
            foreach (Cell c in Group.Cells)
            {
                c.MultGroups.Remove(Group);
            }

            Group = null;
        }
    }
}

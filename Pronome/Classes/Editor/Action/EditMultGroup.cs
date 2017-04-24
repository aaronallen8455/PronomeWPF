namespace Pronome.Editor
{
    public class EditMultGroup : AbstractBeatCodeAction
    {
        protected MultGroup Group;

        protected string Factor;

        public EditMultGroup(MultGroup group, string factor) : base(group.Row, "Edit Multiply Group")
        {
            Group = group;
            Factor = factor;
        }

        protected override void Transformation()
        {
            Group.FactorValue = Factor;

            Group = null;
        }
    }
}

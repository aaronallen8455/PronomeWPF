namespace Pronome.Editor
{
    public class EditRepeatGroup : AbstractBeatCodeAction
    {
        protected int NewTimes;

        protected string NewLtm;

        protected RepeatGroup Group;

        public EditRepeatGroup(RepeatGroup repGroup, int newTimes, string newLtm) : base(repGroup.Row, "Edit Repeat Group")
        {
            Group = repGroup;
            NewTimes = newTimes;
            NewLtm = newLtm;
        }

        protected override void Transformation()
        {
            Group.Times = NewTimes;
            Group.LastTermModifier = NewLtm;
            Group = null;
        }
    }
}

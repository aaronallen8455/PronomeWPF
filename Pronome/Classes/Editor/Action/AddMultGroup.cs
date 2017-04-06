using System.Collections.Generic;
using System.Linq;

namespace Pronome.Editor
{
    //public class AddMultGroup : AbstractAction, IEditorAction
    //{
    //    protected MultGroup Group;
    //
    //    public string HeaderText { get => "Create Multiply Group"; }
    //
    //    public AddMultGroup(Cell[] cells, string factor)
    //    {
    //        Row = cells[0].Row;
    //
    //        Group = new MultGroup();
    //        Group.Row = cells[0].Row;
    //        Group.Factor = factor;
    //        Group.Cells = new LinkedList<Cell>(cells);
    //        Group.Position = cells[0].Position;
    //        Group.Duration = cells.Select(x => x.Duration).Sum();
    //    }
    //
    //    public void Redo()
    //    {
    //        Row.MultGroups.AddLast(Group);
    //        // add to main canvas or a rep canvas
    //        if (Group.Cells.First.Value.RepeatGroups.Count > 0)
    //        {
    //            Group.Cells.First.Value.RepeatGroups.Last.Value.Canvas.Children.Add(Group.Rectangle);
    //            Group.HostCanvas = Group.Cells.First.Value.RepeatGroups.Last.Value.Canvas;
    //        }
    //        else
    //        {
    //            Row.Canvas.Children.Add(Group.Rectangle);
    //            Group.HostCanvas = Row.Canvas;
    //        }
    //
    //        // add the group to all cell's lists in the correct order
    //        Cell cell = Group.Cells.First.Value;
    //        LinkedListNode<MultGroup> before = null;
    //        if (cell.MultGroups.Any())
    //        {
    //            before = cell.MultGroups.Find(cell.MultGroups.Where(x => x.Position <= Group.Position && x.Duration > Group.Duration).Last());
    //
    //        }
    //        // add cells
    //        foreach (Cell c in Group.Cells)
    //        {
    //            if (before != null)
    //            {
    //                c.MultGroups.AddAfter(before, Group);
    //            }
    //            else
    //            {
    //                c.MultGroups.AddLast(Group);
    //            }
    //        }
    //
    //        Row.BeatCodeIsCurrent = false;
    //        EditorWindow.Instance.SetChangesApplied(false);
    //        RedrawReferencers();
    //    }
    //
    //    public void Undo()
    //    {
    //        Row.MultGroups.Remove(Group);
    //
    //        Group.HostCanvas.Children.Remove(Group.Rectangle);
    //
    //        foreach (Cell c in Group.Cells)
    //        {
    //            c.MultGroups.Remove(Group);
    //        }
    //
    //        Row.BeatCodeIsCurrent = false;
    //        EditorWindow.Instance.SetChangesApplied(false);
    //        RedrawReferencers();
    //    }
    //}
    //
    //public class RemoveMultGroup : AbstractAction, IEditorAction
    //{
    //    protected MultGroup Group;
    //
    //    public string HeaderText { get => "Remove Multiply Group"; }
    //
    //    public RemoveMultGroup(MultGroup group)
    //    {
    //        Group = group;
    //    }
    //
    //    public void Redo()
    //    {
    //        // remove from cells and row
    //        foreach (Cell c in Group.Cells)
    //        {
    //            c.MultGroups.Remove(Group);
    //        }
    //        Group.Row.MultGroups.Remove(Group);
    //        Group.HostCanvas.Children.Remove(Group.Rectangle);
    //
    //        Row.BeatCodeIsCurrent = false;
    //        EditorWindow.Instance.SetChangesApplied(false);
    //        RedrawReferencers();
    //    }
    //
    //    public void Undo()
    //    {
    //        Row.MultGroups.AddLast(Group);
    //        // add to main canvas or a rep canvas
    //        if (Group.Cells.First.Value.RepeatGroups.Count > 0)
    //        {
    //            Group.Cells.First.Value.RepeatGroups.Last.Value.Canvas.Children.Add(Group.Rectangle);
    //            Group.HostCanvas = Group.Cells.First.Value.RepeatGroups.Last.Value.Canvas;
    //        }
    //        else
    //        {
    //            Row.Canvas.Children.Add(Group.Rectangle);
    //            Group.HostCanvas = Row.Canvas;
    //        }
    //
    //        // add the group to all cell's lists in the correct order
    //        Cell cell = Group.Cells.First.Value;
    //        LinkedListNode<MultGroup> before = null;
    //        if (cell.MultGroups.Any())
    //        {
    //            before = cell.MultGroups.Find(cell.MultGroups.Where(x => x.Position <= Group.Position && x.Duration > Group.Duration).Last());
    //
    //        }
    //        // add cells
    //        foreach (Cell c in Group.Cells)
    //        {
    //            if (before != null)
    //            {
    //                c.MultGroups.AddAfter(before, Group);
    //            }
    //            else
    //            {
    //                c.MultGroups.AddLast(Group);
    //            }
    //        }
    //
    //        Row.BeatCodeIsCurrent = false;
    //        EditorWindow.Instance.SetChangesApplied(false);
    //        RedrawReferencers();
    //    }
    //}
}

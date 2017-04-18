using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pronome.Editor
{
    interface IGroupCommandHelper
    {
        int TimesAccessed { get; set; }

        bool CanAdd { get; set; }

        bool CanRemoveOrEdit { get; set; }

        Group GroupToRemoveOrEdit { get; set; }
    }

    static class GroupCommandHelper<T> where T : IGroupCommandHelper, new()
    {
        static T Result;

        /// <summary>
        /// The number of commands that can access the result before clearing the cached result.
        /// </summary>
        const int MaxAccessTimes = 3;

        static public Group GetGroupToRemoveOrEdit()
        {
            if (Result != null)
            {
                return Result.GroupToRemoveOrEdit;
            }

            return null;
        }

        static public T GetResult()
        {
            if (Result != null && Result.TimesAccessed++ < MaxAccessTimes)
            {
                return Result;
            }

            Result = new T()
            {
                TimesAccessed = 1
            };
            return Result;
        }
    }

    public class MultGroupCommandHelper : IGroupCommandHelper
    {
        public int TimesAccessed { get; set; }

        public bool CanAdd { get; set; }

        public bool CanRemoveOrEdit { get; set; }

        public Group GroupToRemoveOrEdit { get; set; }

        public MultGroupCommandHelper()
        {
            if (Cell.SelectedCells.Cells.Count == 0)
            {
                CanAdd = false;
                CanRemoveOrEdit = false;
                GroupToRemoveOrEdit = null;
                return;
            }

            Cell first = Cell.SelectedCells.FirstCell;
            Cell last = Cell.SelectedCells.LastCell;

            if (Cell.SelectedCells.Cells.Count == 1)
            {
                // check if single cell selection already has a mult group (not allowed)
                if (first.MultGroups.Any() 
                    && first.MultGroups.Last.Value.Cells.First == first.MultGroups.Last.Value.Cells.Last)
                {
                    CanAdd = false;
                    CanRemoveOrEdit = true;
                    GroupToRemoveOrEdit = first.MultGroups.Last.Value;
                }
                else
                {
                    CanAdd = true;
                    CanRemoveOrEdit = false;
                    GroupToRemoveOrEdit = null;
                }
            }
            else if (first.MultGroups.Any() && last.MultGroups.Any())
            {
                // make sure that if first and last are already in a mult group, that there won't be any intersect
                LinkedListNode<MultGroup> left = first.MultGroups.First;
                LinkedListNode<MultGroup> right = last.MultGroups.First;

                while (left != null && right != null)
                {
                    if (left.Value != right.Value)
                    {
                        CanRemoveOrEdit = false;
                        GroupToRemoveOrEdit = null;
                        // must be endpoints
                        if (left == null)
                        {
                            // right group must end on last cell of select
                            if (right.Value.Cells.Last.Value == last)
                            {
                                CanAdd = true;
                                break;
                            }
                            else
                            {
                                CanAdd = false;
                                break;
                            }
                        }
                        else if (right == null)
                        {
                            // left group must start on first cell of select
                            if (left.Value.Cells.First.Value == first)
                            {
                                CanAdd = true;
                                break;
                            }
                            else
                            {
                                CanAdd = false;
                                break;
                            }
                        }
                        // both groups must start and end appropriately
                        else if (left.Value.Cells.First.Value == first && right.Value.Cells.Last.Value == last)
                        {
                            CanAdd = true;
                            break;
                        }
                        else
                        {
                            CanAdd = false;
                            break;
                        }
                    }
                    else
                    {
                        if (left.Value.Cells.First.Value == first && right.Value.Cells.Last.Value == last)
                        {
                            CanAdd = false;
                            CanRemoveOrEdit = true;
                            GroupToRemoveOrEdit = left.Value;
                            break;
                        }
                        else
                        {
                            CanAdd = true;
                            CanRemoveOrEdit = false;
                            GroupToRemoveOrEdit = null;
                            // don't break, need to see if nested group also passes.
                        }
                    }

                    left = left.Next;
                    right = right.Next;
                }
            }
            else
            {
                // selection has no Mult groups
                CanAdd = true;
                CanRemoveOrEdit = false;
                GroupToRemoveOrEdit = null;
            }
        }
    }

    public class RepeatGroupCommandHelper : IGroupCommandHelper
    {
        public int TimesAccessed { get; set; }

        public bool CanAdd { get; set; }

        public bool CanRemoveOrEdit { get; set; }

        public Group GroupToRemoveOrEdit { get; set; }

        public RepeatGroupCommandHelper()
        {
            if (Cell.SelectedCells.Cells.Count == 0)
            {
                CanAdd = false;
                CanRemoveOrEdit = false;
                GroupToRemoveOrEdit = null;
                return;
            }

            Cell first = Cell.SelectedCells.FirstCell;
            Cell last = Cell.SelectedCells.LastCell;

            if (Cell.SelectedCells.Cells.Count == 1)
            {
                // check if single cell selection already has a mult group (not allowed)
                if (first.RepeatGroups.Any()
                    && first.RepeatGroups.Last.Value.Cells.First == first.RepeatGroups.Last.Value.Cells.Last)
                {
                    CanAdd = false;
                    CanRemoveOrEdit = true;
                    GroupToRemoveOrEdit = first.RepeatGroups.Last.Value;
                }
                else
                {
                    CanAdd = true;
                    CanRemoveOrEdit = false;
                    GroupToRemoveOrEdit = null;
                }
            }
            else if (first.RepeatGroups.Any() && last.RepeatGroups.Any())
            {
                // make sure that if first and last are already in a mult group, that there won't be any intersect
                LinkedListNode<RepeatGroup> left = first.RepeatGroups.First;
                LinkedListNode<RepeatGroup> right = last.RepeatGroups.First;

                while (left != null && right != null)
                {
                    if (left.Value != right.Value)
                    {
                        CanRemoveOrEdit = false;
                        GroupToRemoveOrEdit = null;
                        // must be endpoints
                        if (left == null)
                        {
                            // right group must end on last cell of select
                            if (right.Value.Cells.Last.Value == last)
                            {
                                CanAdd = true;
                                break;
                            }
                            else
                            {
                                CanAdd = false;
                                break;
                            }
                        }
                        else if (right == null)
                        {
                            // left group must start on first cell of select
                            if (left.Value.Cells.First.Value == first)
                            {
                                CanAdd = true;
                                break;
                            }
                            else
                            {
                                CanAdd = false;
                                break;
                            }
                        }
                        // both groups must start and end appropriately
                        else if (left.Value.Cells.First.Value == first && right.Value.Cells.Last.Value == last)
                        {
                            CanAdd = true;
                            break;
                        }
                        else
                        {
                            CanAdd = false;
                            break;
                        }
                    }
                    else
                    {
                        if (left.Value.Cells.First.Value == first && right.Value.Cells.Last.Value == last)
                        {
                            CanAdd = false;
                            CanRemoveOrEdit = true;
                            GroupToRemoveOrEdit = left.Value;
                            break;
                        }
                        else
                        {
                            CanAdd = true;
                            CanRemoveOrEdit = false;
                            GroupToRemoveOrEdit = null;
                            // don't break, need to see if nested group also passes.
                        }
                    }

                    left = left.Next;
                    right = right.Next;
                }
            }
            else
            {
                // selection has no Mult groups
                CanAdd = true;
                CanRemoveOrEdit = false;
                GroupToRemoveOrEdit = null;
            }
            //if (Cell.SelectedCells.Cells.Count == 0)
            //{
            //    CanAdd = false;
            //    CanRemoveOrEdit = false;
            //    GroupToRemoveOrEdit = null;
            //    return;
            //}
            //
            //if (Cell.SelectedCells.Cells.Count == 1)
            //{
            //    // if a single cell selected, no further validation
            //    if (Cell.SelectedCells.FirstCell.RepeatGroups.Any() &&
            //        Cell.SelectedCells.Cells[0].RepeatGroups.Last.Value.Cells.First == Cell.SelectedCells.Cells[0].RepeatGroups.Last.Value.Cells.Last)
            //    {
            //        // not if a single cell repeat already exists over this cell.
            //        //e.CanExecute = false;
            //        CanAdd = false;
            //        CanRemoveOrEdit = true;
            //        GroupToRemoveOrEdit = Cell.SelectedCells.FirstCell.RepeatGroups.Last.Value;
            //        return;
            //    }
            //    else
            //    {
            //        CanAdd = true;
            //        CanRemoveOrEdit = false;
            //        GroupToRemoveOrEdit = null;
            //        return;
            //    }
            //}
            //
            //if (Cell.SelectedCells.FirstCell.RepeatGroups.Any() || Cell.SelectedCells.LastCell.RepeatGroups.Any())
            //{
            //    // Ensure that the selected cells share grouping scope
            //    LinkedListNode<RepeatGroup> first = Cell.SelectedCells.FirstCell.RepeatGroups.First;
            //    LinkedListNode<RepeatGroup> last = Cell.SelectedCells.LastCell.RepeatGroups.First;
            //    while (true)
            //    {
            //        // both cells share this group, go to nested group
            //        if (first != null && last != null)
            //        {
            //            if (first.Value == last.Value)
            //            {
            //                // don't allow a repeat group to be made right on top of another RG
            //                if (first.Value.Cells.First.Value != Cell.SelectedCells.FirstCell
            //                    && first.Value.Cells.Last.Value != Cell.SelectedCells.LastCell)
            //                {
            //                    first = first.Next;
            //                    last = last.Next;
            //                }
            //                else
            //                {
            //                    CanAdd = false;
            //                    CanRemoveOrEdit = true;
            //                    GroupToRemoveOrEdit = first.Value;
            //                    break;
            //                }
            //            }
            //            else if (first.Value.Cells.First.Value == Cell.SelectedCells.FirstCell &&
            //                    last.Value.Cells.Last.Value == Cell.SelectedCells.LastCell)
            //            {
            //                // both ends of select are in different groups but those groups are not being cut
            //                CanAdd = true;
            //                CanRemoveOrEdit = false;
            //                GroupToRemoveOrEdit = null;
            //                break;
            //            }
            //            else
            //            {
            //                CanAdd = false;
            //                CanRemoveOrEdit = false;
            //                GroupToRemoveOrEdit = null;
            //                break;
            //            }
            //        }
            //        // is last cell in nested repeat group where it is the last cell?
            //        else if (first == null && last != null)
            //        {
            //            if (last.Value.Cells.Last.Value == Cell.SelectedCells.LastCell)
            //            {
            //                CanAdd = true;
            //                CanRemoveOrEdit = false;
            //                GroupToRemoveOrEdit = null;
            //                break;
            //            }
            //            else
            //            {
            //                CanAdd = false;
            //                CanRemoveOrEdit = false;
            //                GroupToRemoveOrEdit = null;
            //                break;
            //            }
            //        }
            //        // is first cell in nested rep group and is the first cell of that group?
            //        else if (first != null && last == null)
            //        {
            //            if (first.Value.Cells.First.Value == Cell.SelectedCells.FirstCell)
            //            {
            //                CanAdd = true;
            //                CanRemoveOrEdit = false;
            //                GroupToRemoveOrEdit = null;
            //                break;
            //            }
            //            else
            //            {
            //                CanAdd = false;
            //                CanRemoveOrEdit = false;
            //                GroupToRemoveOrEdit = null;
            //                break;
            //            }
            //        }
            //
            //        // reached the end
            //        if (first == null && last == null)
            //        {
            //            CanAdd = true;
            //            CanRemoveOrEdit = false;
            //            GroupToRemoveOrEdit = null;
            //            break;
            //        }
            //    }
            //}
        }
    }
}

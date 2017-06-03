using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace Pronome
{
    class TaskDialogWrapper
    {
        protected Window Parent;

        public TaskDialogWrapper (Window parent)
        {
            Parent = parent;
        }

        public TaskDialogResult Show (string title, string mainInstruction, string content, TaskDialogButtons buttons, TaskDialogIcon icon)
        {
            return TaskDialog(
                new System.Windows.Interop.WindowInteropHelper(Parent).Handle,
                IntPtr.Zero,
                title,
                mainInstruction,
                content,
                buttons,
                icon);
        }

        public enum TaskDialogResult
        {
            Ok = 1,
            Cancel = 2,
            Retry = 4,
            Yes = 6,
            No = 7,
            Close = 8
        }

        [Flags]
        public enum TaskDialogButtons
        {
            Ok = 0x0001,
            Yes = 0x0002,
            No = 0x0004,
            Cancel = 0x0008,
            Retry = 0x0010,
            Close = 0x0020
        }

        public enum TaskDialogIcon
        {
            Warning = 65535,
            Error = 65534,
            Information = 65533,
            Shield = 65532
        }

        [DllImport("comctl32.dll", PreserveSig = false, CharSet = CharSet.Unicode)]
        protected static extern TaskDialogResult TaskDialog(
            IntPtr hwndParent,
            IntPtr hInstance,
            string title,
            string mainInstruction,
            string content,
            TaskDialogButtons buttons,
            TaskDialogIcon icon);
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Pronome
{
    class OptionsWindow : Window
    {
        /**<summary>Window hides instead of closes if this is true.</summary>*/
        public bool KeepOpen = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (KeepOpen)
            {
                Hide();
                e.Cancel = true;
            }
        }
    }
}

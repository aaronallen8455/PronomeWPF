using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            EventManager.RegisterClassHandler(
                typeof(TextBox),
                TextBox.KeyUpEvent,
                new System.Windows.Input.KeyEventHandler(TextBox_KeyUp)
            );
            
            base.OnStartup(e);
        }
        
        private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;

            ((TextBox)sender).RaiseEvent(new RoutedEventArgs(TextBox.LostFocusEvent));
        }

        private void TextEditor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Don't allow beat code content to be altered while playing.
            //if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
            //    e.Handled = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace Pronome
{
    class LayerPanel : WrapPanel
    {
        public Layer Layer;

        public TextEditor textEditor;

        protected Panel layerList;

        protected Button deleteButton;

        public LayerPanel(Panel Parent) : base()
        {
            // add to the layerlist panel
            layerList = Parent;
            layerList.Children.Add(this);

            // create the layer
            Layer = new Layer("1");

            // init the text editor
            textEditor = new TextEditor();
            textEditor.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            textEditor.FontSize = 20;
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Pronome");
            textEditor.LostFocus += new RoutedEventHandler(textEditor_LostFocus);
            this.Children.Add(textEditor);

            // source selector control

            // pitch field (used if source is a pitch)

            // volume control

            // pan control

            // offset control

            // mute control

            // solo control

            // delete button
            deleteButton = new Button();
            TextBlock text = new TextBlock();
            text.Text = "Remove";
            deleteButton.Content = deleteButton;
            deleteButton.Click += new RoutedEventHandler(deleteButton_Click);
        }

        protected void textEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            // TODO: validate the beat string

            if (Layer.ParsedString != textEditor.Text)
            {
                Layer.Parse(textEditor.Text);
            }
        }

        protected void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            // dispose the layer
            Layer.Dispose();
            // remove the panel
            layerList.Children.Remove(this);
        }
    }
}

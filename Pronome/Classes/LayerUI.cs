using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;

namespace Pronome
{
    /**<summary>Provides the user interface for a beat layer.</summary>*/
    class LayerUI
    {
        public Layer Layer;

        public Grid basePanel;

        public TextEditor textEditor;

        protected WrapPanel controlPanel;

        protected Panel layerList;

        protected ComboBox baseSourceSelector;

        protected TextBox pitchInput;

        protected Slider volumeSlider;

        protected Slider panSlider;

        protected TextBox offsetInput;

        protected ToggleButton muteButton;

        protected ToggleButton soloButton;

        protected Button deleteButton;

        /**<summary>Constructor</summary>
         * <param name="Parent">The list to add the UI to.</param>
         */
        public LayerUI(Panel Parent)
        {
            ResourceDictionary resources = Application.Current.Resources;

            layerList = Parent;
            // add base panel to parent layerlist panel
            basePanel = resources["layerGrid"] as Grid;
            layerList.Children.Add(basePanel);

            // add the panel that has the main controls
            controlPanel = resources["layerWrap"] as WrapPanel;
            basePanel.Children.Add(controlPanel);

            // create the layer
            Layer = new Layer("1");

            // init the text editor
            textEditor = resources["textEditor"] as TextEditor;
            textEditor.LostFocus += new RoutedEventHandler(textEditor_LostFocus);
            controlPanel.Children.Add(textEditor);
            MakeLabel("Beat", textEditor);

            // source selector control
            baseSourceSelector = resources["sourceSelector"] as ComboBox;
            controlPanel.Children.Add(baseSourceSelector);
            MakeLabel("Source", baseSourceSelector);
            // get array of sources
            // TODO: add index numbers to .wav items
            List<string> sources = WavFileStream.FileNameIndex.Cast<string>().ToList();
            sources = sources.Where((n, i) => i % 2 == 1).ToList(); // get the pretty names from the odd numbered indexes
            sources[0] = "Pitch"; // replace Silentbeat with Pitch
            baseSourceSelector.ItemsSource = sources;
            baseSourceSelector.SelectedIndex = 0;
            baseSourceSelector.SelectionChanged += new SelectionChangedEventHandler(baseSourceSelector_SelectionChanged);

            // pitch field (used if source is a pitch)
            pitchInput = resources["pitchInput"] as TextBox;
            controlPanel.Children.Add(pitchInput);
            MakeLabel("Note", pitchInput);
            if (baseSourceSelector.SelectedItem as string == "Pitch")
            {
                pitchInput.Text = Layer.BaseSourceName;
            }
            else pitchInput.Visibility = Visibility.Collapsed;
            pitchInput.TextChanged += new TextChangedEventHandler(pitchInput_TextChanged);

            // volume control
            volumeSlider = resources["volumeControl"] as Slider;
            controlPanel.Children.Add(volumeSlider);
            MakeLabel("Vol.", volumeSlider);
            volumeSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(volumeSlider_ValueChanged);

            // pan control
            panSlider = resources["panControl"] as Slider;
            controlPanel.Children.Add(panSlider);
            MakeLabel("Pan", panSlider);
            panSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(panSlider_ValueChanged);

            // offset control
            offsetInput = resources["offsetInput"] as TextBox;
            controlPanel.Children.Add(offsetInput);
            MakeLabel("Offset", offsetInput);
            offsetInput.TextChanged += new TextChangedEventHandler(offsetInput_TextChanged);

            // mute control
            muteButton = resources["muteButton"] as ToggleButton;
            controlPanel.Children.Add(muteButton);
            muteButton.Checked += new RoutedEventHandler(muteButton_Checked);
            muteButton.Unchecked += new RoutedEventHandler(muteButton_Checked);

            // solo control
            soloButton = resources["soloButton"] as ToggleButton;
            controlPanel.Children.Add(soloButton);
            soloButton.Checked += new RoutedEventHandler(soloButton_Checked);
            soloButton.Unchecked += new RoutedEventHandler(soloButton_Checked);

            // delete button
            deleteButton = resources["deleteButton"] as Button;
            controlPanel.Children.Add(deleteButton);
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

        protected void baseSourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string newSource = WavFileStream.GetFileByName(baseSourceSelector.SelectedItem as string);
            // set new base source
            if (newSource != Layer.BaseSourceName)
            {
                if (newSource == "Pitch")
                {
                    pitchInput.Visibility = Visibility.Visible;
                    // use contents of pitch field as source
                    Layer.SetBaseSource(pitchInput.Text);
                }
                else
                {
                    pitchInput.Visibility = Visibility.Collapsed;
                    Layer.SetBaseSource(newSource);
                }
            }
        }

        protected void pitchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // validate input
            if (Regex.IsMatch(pitchInput.Text, @"^[A-Ga-g][#b]?[\d]$|^[\d.]+$"))
            {
                Layer.SetBaseSource(pitchInput.Text);
            }
        }

        protected void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Layer.Volume != volumeSlider.Value)
            {
                Layer.Volume = (float)volumeSlider.Value;
            }
        }

        protected void panSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Layer.Pan = (float)panSlider.Value;
        }

        protected void offsetInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            //validate
            if (Regex.IsMatch(offsetInput.Text, @"[\d+\-*/xX.]*"))
            {
                Layer.SetOffset(BeatCell.Parse(offsetInput.Text));
            }
        }

        protected void muteButton_Checked(object sender, RoutedEventArgs e)
        {
            Layer.ToggleMute();
        }

        protected void soloButton_Checked(object sender, RoutedEventArgs e)
        {
            Layer.ToggleSoloGroup();
        }

        protected void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            // dispose the layer
            Layer.Dispose();
            // remove the panel
            layerList.Children.Remove(basePanel);
        }

        /**<summary>Make a label for the given element</summary>*/
        protected void MakeLabel(string labelText, UIElement element)
        {
            Label label = new Label();
            var text = new TextBlock();
            text.Text = labelText;
            label.Content = text;
            label.Target = element;
            controlPanel.Children.Add(label);
        }
    }
}

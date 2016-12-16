using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace Pronome
{
    class LayerPanel : Grid
    {
        public Layer Layer;

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

        public LayerPanel(Panel Parent) : base()
        {
            // add to the layerlist panel
            layerList = Parent;
            layerList.Children.Add(this);

            // add the panel that has the main controls
            controlPanel = new WrapPanel();
            this.Children.Add(controlPanel);

            // create the layer
            Layer = new Layer("1");

            // init the text editor
            textEditor = new TextEditor();
            textEditor.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            textEditor.FontSize = 20;
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Pronome");
            textEditor.LostFocus += new RoutedEventHandler(textEditor_LostFocus);
            controlPanel.Children.Add(textEditor);

            // source selector control
            baseSourceSelector = new ComboBox();
            controlPanel.Children.Add(baseSourceSelector);
            // get array of sources
            List<string> sources = WavFileStream.FileNameIndex.Cast<string>().ToList();
            sources = sources.Where((n, i) => i % 2 == 1).ToList(); // get the pretty names from the odd numbered indexes
            sources[0] = "Pitch"; // replace Silentbeat with Pitch
            baseSourceSelector.ItemsSource = sources;
            baseSourceSelector.SelectedIndex = 0;
            baseSourceSelector.SelectionChanged += new SelectionChangedEventHandler(baseSourceSelector_SelectionChanged);

            // pitch field (used if source is a pitch)
            pitchInput = new TextBox();
            controlPanel.Children.Add(pitchInput);
            pitchInput.Width = 50;
            if (baseSourceSelector.SelectedItem as string == "Pitch")
            {
                pitchInput.Text = Layer.BaseSourceName;
            }
            pitchInput.TextChanged += new TextChangedEventHandler(pitchInput_TextChanged);

            // volume control
            volumeSlider = new Slider();
            controlPanel.Children.Add(volumeSlider);
            volumeSlider.Minimum = 0;
            volumeSlider.Maximum = 1;
            volumeSlider.Value = 1;
            volumeSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(volumeSlider_ValueChanged);

            // pan control
            panSlider = new Slider();
            controlPanel.Children.Add(panSlider);
            panSlider.Minimum = -1;
            panSlider.Maximum = 1;
            panSlider.Value = 0;
            panSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(panSlider_ValueChanged);

            // offset control
            offsetInput = new TextBox();
            offsetInput.Width = 50;
            controlPanel.Children.Add(offsetInput);
            offsetInput.TextChanged += new TextChangedEventHandler(offsetInput_TextChanged);

            // mute control
            muteButton = new ToggleButton();
            TextBlock muteText = new TextBlock();
            muteText.Text = "Mute";
            muteButton.Content = muteText;
            controlPanel.Children.Add(muteButton);
            muteButton.Checked += new RoutedEventHandler(muteButton_Checked);
            muteButton.Unchecked += new RoutedEventHandler(muteButton_Checked);

            // solo control
            soloButton = new ToggleButton();
            TextBlock soloText = new TextBlock();
            soloText.Text = "Solo";
            soloButton.Content = soloText;
            controlPanel.Children.Add(soloButton);
            soloButton.Checked += new RoutedEventHandler(soloButton_Checked);
            soloButton.Unchecked += new RoutedEventHandler(soloButton_Checked);

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

        protected void baseSourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string newSource = WavFileStream.GetFileByName(baseSourceSelector.SelectedItem as string);
            // set new base source
            if (newSource != Layer.BaseSourceName)
            {
                if (newSource == "Pitch")
                {
                    // use contents of pitch field as source
                    Layer.SetBaseSource(pitchInput.Text);
                }
                else
                    Layer.SetBaseSource(newSource);
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
            layerList.Children.Remove(this);
        }
    }
}

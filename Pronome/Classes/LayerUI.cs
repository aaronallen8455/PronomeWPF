using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System;
using System.ComponentModel.Design;
using ICSharpCode.AvalonEdit;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.AvalonEdit.AddIn;

namespace Pronome
{
    /**<summary>Provides the user interface for a beat layer.</summary>*/
    class LayerUI
    {
        public static List<LayerUI> Items = new List<LayerUI>();

        public Layer Layer;

        public Grid basePanel;

        public TextEditor textEditor;

        protected Grid controlGrid;

        protected WrapPanel controlPanel;

        protected Panel layerList;

        protected ComboBox baseSourceSelector;

        protected TextBox pitchInput;

        protected Slider volumeSlider;

        protected Slider panSlider;

        protected TextBox offsetInput;

        protected StackPanel buttonSidePanel;

        protected ToggleButton muteButton;

        protected ToggleButton soloButton;

        protected TextBlock indexLabel;

        protected Rectangle backgroundRect;

        protected Button deleteButton;

        ITextMarkerService textMarkerService;

        /**<summary>Constructor</summary>
         * <param name="Parent">The list to add the UI to.</param>
         */
        public LayerUI(Panel Parent, Layer layer = null)
        {
            Items.Add(this);

            ResourceDictionary resources = Application.Current.Resources;

            layerList = Parent;
            // add base panel to parent layerlist panel
            basePanel = resources["layerGrid"] as Grid;
            layerList.Children.Add(basePanel);

            // add background
            backgroundRect = resources["backgroundRect"] as Rectangle;
            // set to different colors for odd numbered
            backgroundRect.Fill = Items.IndexOf(this) % 2 == 0 ? Brushes.SteelBlue : Brushes.DarkCyan;
            basePanel.Children.Add(backgroundRect);

            // create the layer if doesn't exist
            if (layer == null)
                Layer = new Layer("1");
            else Layer = layer;

            // the grid that seperates beat input from other controls
            controlGrid = resources["controlGrid"] as Grid;
            basePanel.Children.Add(controlGrid);

            // add the panel that has the main controls
            controlPanel = resources["layerWrap"] as WrapPanel;
            controlGrid.Children.Add(controlPanel);

            // init the text editor
            textEditor = resources["textEditor"] as TextEditor;
            // init markup utility
            var textMarkerService = new TextMarkerService(textEditor.Document);
            textEditor.TextArea.TextView.BackgroundRenderers.Add(textMarkerService);
            textEditor.TextArea.TextView.LineTransformers.Add(textMarkerService);
            IServiceContainer services = (IServiceContainer)textEditor.Document.ServiceProvider.GetService(typeof(IServiceContainer));
            if (services != null)
                services.AddService(typeof(ITextMarkerService), textMarkerService);
            this.textMarkerService = textMarkerService;

            textEditor.Text = Layer.ParsedString;
            textEditor.LostFocus += new RoutedEventHandler(textEditor_LostFocus);
            MakeLabel("Beat Code", textEditor, true);

            // source selector control
            baseSourceSelector = resources["sourceSelector"] as ComboBox;
            MakeLabel("Source", baseSourceSelector);
            // get array of sources
            // TODO: add index numbers to .wav items
            List<string> sources = WavFileStream.FileNameIndex.Cast<string>()
                .Where((n, i) => i % 2 == 1) // get the pretty names from the odd numbered indexes
                .Select((x, i) => (i.ToString() + ".").PadRight(4) + x).ToList(); // add index numbers
            sources[0] = "Pitch"; // replace Silentbeat with Pitch
            baseSourceSelector.ItemsSource = sources;
            if (Layer.BaseSourceName.First() == 'w') // if wav source get the the selector name from file name
            {
                string selector = WavFileStream.GetSelectorNameByFile(Layer.BaseSourceName);
                if (sources.Contains(selector))
                {
                    baseSourceSelector.SelectedIndex = sources.IndexOf(selector);
                }
                else baseSourceSelector.SelectedIndex = 0;
            }
            else
            {
                baseSourceSelector.SelectedIndex = 0; // pitch source
            }
            baseSourceSelector.SelectionChanged += new SelectionChangedEventHandler(baseSourceSelector_SelectionChanged);

            // pitch field (used if source is a pitch)
            pitchInput = resources["pitchInput"] as TextBox;
            MakeLabel("Note", pitchInput);
            if (baseSourceSelector.SelectedItem as string == "Pitch")
            {
                pitchInput.Text = Layer.BaseSourceName;
            }
            else
            {
                pitchInput.Text = Layer.GetAutoPitch();
                (pitchInput.Parent as StackPanel).Visibility = Visibility.Collapsed;
            }
            pitchInput.LostFocus += new RoutedEventHandler(pitchInput_LostFocus);

            // volume control
            volumeSlider = resources["volumeControl"] as Slider;
            volumeSlider.Value = Layer.Volume;
            MakeLabel("Vol.", volumeSlider);
            volumeSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(volumeSlider_ValueChanged);

            // pan control
            panSlider = resources["panControl"] as Slider;
            panSlider.Value = Layer.Pan;
            MakeLabel("Pan", panSlider);
            panSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(panSlider_ValueChanged);

            // offset control
            offsetInput = resources["offsetInput"] as TextBox;
            offsetInput.Text = Layer.ParsedOffset;
            MakeLabel("Offset", offsetInput);
            offsetInput.LostFocus += new RoutedEventHandler(offsetInput_LostFocus);

            // side panel
            buttonSidePanel = resources["layerLeftStackPanel"] as StackPanel;
            basePanel.Children.Add(buttonSidePanel);

            // mute control
            muteButton = resources["muteButton"] as ToggleButton;
            buttonSidePanel.Children.Add(muteButton);
            muteButton.Checked += new RoutedEventHandler(muteButton_Checked);
            muteButton.Unchecked += new RoutedEventHandler(muteButton_Checked);

            // solo control
            soloButton = resources["soloButton"] as ToggleButton;
            buttonSidePanel.Children.Add(soloButton);
            soloButton.Checked += new RoutedEventHandler(soloButton_Checked);
            soloButton.Unchecked += new RoutedEventHandler(soloButton_Checked);

            // Layer index label
            indexLabel = resources["layerIndexLabel"] as TextBlock;
            indexLabel.Text = (Items.IndexOf(this) + 1).ToString();
            basePanel.Children.Add(indexLabel);

            // delete button
            deleteButton = resources["deleteButton"] as Button;
            basePanel.Children.Add(deleteButton);
            deleteButton.Click += new RoutedEventHandler(deleteButton_Click);
        }

        protected void textEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            // TODO: validate the beat string
            if (textEditor.Text == "")
            {
                textEditor.Text = "1";
            }

            if (Layer.ParsedString != textEditor.Text)
            {
                try
                {
                    string errorMsg = ValidateBeatCode();

                    if (errorMsg == string.Empty)
                    {
                        Layer.Parse(textEditor.Text);
                    }
                    else throw new BeatSyntaxException(errorMsg);
                }
                catch (BeatSyntaxException ex)
                {
                    MessageBox.Show("Please fix the following errors:\r\r" + ex.Message, "Beat Code Contains Errors", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                //catch (Exception ex)
                //{
                //    MessageBox.Show($"This Beat contains a value that is too large ({ex.Message}). Use smaller values.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Error);
                //}
            }
        }

        protected void baseSourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (baseSourceSelector.SelectedItem as string == "Pitch")
            {
                (pitchInput.Parent as StackPanel).Visibility = Visibility.Visible;
                // use contents of pitch field as source
                //Layer.SetBaseSource(pitchInput.Text);
                Layer.NewBaseSource(pitchInput.Text);
            }
            else
            {
                string newSource = WavFileStream.GetFileByName((baseSourceSelector.SelectedItem as string).Substring(4));
                // set new base source
                if (newSource != Layer.BaseSourceName)
                {
                    (pitchInput.Parent as StackPanel).Visibility = Visibility.Collapsed;
                    //Layer.SetBaseSource(newSource);
                    Layer.NewBaseSource(newSource);
                }
            }
        }

        protected void pitchInput_LostFocus(object sender, RoutedEventArgs e)
        {
            // validate input
            if (Regex.IsMatch(pitchInput.Text, @"^[A-Ga-g][#b]?[\d]$|^[\d.]+$"))
            {
                //Layer.SetBaseSource(pitchInput.Text);
                Layer.NewBaseSource(pitchInput.Text);
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

        protected void offsetInput_LostFocus(object sender, RoutedEventArgs e)
        {
            //validate
            if (Regex.IsMatch(offsetInput.Text, @"[\d+\-*/xX.]+"))
            {
                Layer.SetOffset(BeatCell.Parse(offsetInput.Text));
                Layer.ParsedOffset = offsetInput.Text;
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
            // if no more layers, stop the playback
            if (Metronome.GetInstance().Layers.Count == 1)
            {
                (Application.Current.MainWindow as MainWindow).stopButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            // dispose the layer
            Layer.Dispose();
            Remove();
        }

        /**<summary>Remove this item from the interface.</summary>*/
        public void Remove()
        {
            // remove the panel
            layerList.Children.Remove(basePanel);
            int index = Items.IndexOf(this);
            Items.Remove(this);

            // update index labels and background color
            for (int i=index; i<Items.Count; i++)
            {
                Items[i].indexLabel.Text = (i + 1).ToString();
                Items[i].backgroundRect.Fill = i % 2 == 0 ? Brushes.SteelBlue : Brushes.DarkCyan;
            }
        }

        /**<summary>Make a label for the given element</summary>*/
        protected void MakeLabel(string labelText, UIElement element, bool isBeatInput = false)
        {
            Label label = Application.Current.Resources["label"] as Label;
            var text = new TextBlock();
            text.Text = labelText;
            label.Content = text;
            label.Target = element;

            StackPanel panel = Application.Current.Resources["labelControlPanel"] as StackPanel;
            panel.Children.Add(label);
            panel.Children.Add(element);

            if (isBeatInput)
            {
                panel.SetValue(Grid.RowProperty, 0);
                controlGrid.Children.Add(panel);
            }
            else
                controlPanel.Children.Add(panel);
        }

        public string ValidateBeatCode()
        {
            textMarkerService.RemoveAll(x => true);

            List<string> errorMessages = new List<string>();
            string beatCode = textEditor.Text;

            Dictionary<int, int> commentPos = new Dictionary<int, int>();

            while (Regex.IsMatch(beatCode, @"!.*?!|\s")) // replace comments with spaces
            {
                Match m = Regex.Match(beatCode, @"!.*?!|[\r\n ]");

                int index;
                int length;

                if (m.Value != " " && m.Value.Trim() == string.Empty)
                {
                    index = m.Index - 1;
                    length = m.Length + 1;
                    beatCode = Regex.Replace(beatCode.Substring(0, m.Index + m.Length), @"!.*?!|\s", "") + beatCode.Substring(m.Index + m.Length + 1);

                }
                else
                {
                    index = m.Index;
                    length = m.Length;
                    Regex rex = new Regex(@"!.*?!| ");
                    beatCode = rex.Replace(beatCode, "", 1);
                }

                if (commentPos.ContainsKey(index))
                    commentPos[index] += length;
                else
                    commentPos.Add(index, length);

                //foreach (Match m in mc)
                //{
                //    //m = Regex.Match(beatCode, @"[\r\n]");
                //    //commentPos.Add(m.Index, m.Length + 1);
                //}
            }

            for (int i=0; i<beatTests.GetLength(0); i++)
            {
                string test = beatTests[i, 0];
                string msg = beatTests[i, 1];

                var matches = Regex.Matches(beatCode, test);

                // underline syntax and add to error message
                if (matches.Count > 0)
                {
                    errorMessages.Add(msg);

                    foreach (Match match in matches)
                    {
                        // add back in comment space
                        int off = match.Index;
                        int index = commentPos.Where(x => x.Key < off).Select(x => x.Value).Sum() + off;
                        ITextMarker marker = textMarkerService.Create(index, match.Length);
                        marker.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
                        marker.MarkerColor = Colors.Red;
                    }
                }
            }

            return String.Join("\r", errorMessages.Select(x => "- " + x));
        }

        protected static string[,] beatTests =
        {
            {
                @"\)[^,\]@\\|]*\(|" +
                @"\)[^,@]*[a-z]+[^,]*", "Invalid final repeat modifier"
            },
            {
                @"\(\d*[^\d)]+\d*\)|" +
                @"\(\)", "The number of repeats must be an integer."
            },
            {
                @"][^\d(]|" +
                @"]$", "Missing 'n' value for multi-cell repeat."
            },
            {
                @",$|"+
                @",\s*,|"+
                @"^,|"+
                @"^$|"+
                @",\(|"+
                @",]", "Empty beat cell."
            },
            {
                @"^\[?[^\d$]+,|"+
                @",\[?[^\d$]+,|"+
                @",[^,\d\ss]+$|"+
                @"\d[a-wyzA-WYZ]|"+
                @"[+\-*xX/][^\d.]|"+
                @"[^\d).\s,s]$|"+
                @"[^\d$]\.\D|"+
                @"[^\d$]\.$|"+
                @"\.\d+\.|"+
                @"^[a-zA-Z]|"+
                @",[a-zA-Z]", "Invalid beat cell value."
            },
            {
                @"@[^a-gA-G\dPp]|"+
                @"@[a-gA-G]?[b#]?$|"+
                @"@[a-gA-G][^#b\d]", "Invalid pitch assignment using '@.'"
            },
            {
                @"[^\[,}{]\[", "Incorrect multi-cell repeat syntax"
            },
            {
                @"}[\d.+\-\/*Xx]*[^\d.+\-\/*Xx,|"+
                @"\(\]}][\d.+\-/*Xx]*|"+
                @"}[^\d.]", "Invalid group multiplication coefficient."
            },
            {
                @"\{[^}]*$", "Missing the closing brace of a multiplication group."
            },
            {
                @"^[^{]*}", "Missing the opening brace of a multiplication group."
            },
            {
                @"\[[^\]]*$", "Missing the closing brace of a multi-cell repeat."
            },
            {
                @"^[^\[]*]", "Missingthe opening brace of a multi-cell repeat."
            },
            {
                @"[^|,\[{]\$", "Invalid use of beat reference."
            },
            {
                @"\$[^\ds]|"+
                @"\$[\ds]+[^,|\]}(]", "Invalid beat reference."
            }
        };
    }

    class BeatSyntaxException : Exception
    {
        public BeatSyntaxException(string message) : base(message) { }
    }
}

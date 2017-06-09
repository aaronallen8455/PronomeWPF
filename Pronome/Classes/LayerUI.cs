using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System;
using System.ComponentModel.Design;
using ICSharpCode.AvalonEdit;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.AvalonEdit.AddIn;
using System.Runtime.InteropServices;

namespace Pronome
{
    /**<summary>Provides the user interface for a beat layer.</summary>*/
    public class LayerUI
    {
        public static List<LayerUI> Items = new List<LayerUI>();

        public Layer Layer;

        public Grid basePanel;

        public TextEditor textEditor;

        protected Grid controlGrid;

        protected WrapPanel controlPanel;

        protected Panel layerList;

        public ComboBoxFiltered baseSourceSelector;

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
            Layer.UI = this;

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
            baseSourceSelector = resources["sourceSelector"] as ComboBoxFiltered;
            MakeLabel("Source", baseSourceSelector);
            // get array of sources
            //List<string> sources = InternalSource.Library.Select(x => x.ToString()).ToList();
            ////List<string> sources = WavFileStream.FileNameIndex.Cast<string>()
            ////    .Where((n, i) => i % 2 == 1) // get the pretty names from the odd numbered indexes
            ////    .Select((x, i) => (i.ToString() + ".").PadRight(4) + x).ToList(); // add index numbers
            //sources[0] = "Pitch"; // replace Silentbeat with Pitch
            //sources.AddRange(UserSource.Library.OrderBy(x => x.Label).Select(x => x.ToString())); // add custom sources
            //baseSourceSelector.ItemsSource = sources;

            //if (!PitchStream.IsPitchSourceName(Layer.BaseSourceName)) // if wav source get the the selector name from file name
            //{
                var src = Layer.BaseAudioSource.SoundSource;
                if (src.IsPitch)
                {
                    // Pitch is the first item in the collection
                    baseSourceSelector.SelectedIndex = 0;
                }
                else
                {
                    baseSourceSelector.SelectedItem = src;
                }
                //string selector = WavFileStream.GetSelectorNameByFile(Layer.BaseSourceName);
                //if (sources.Contains(selector))
                //{
                //    baseSourceSelector.SelectedIndex = sources.IndexOf(selector);
                //}
                //else baseSourceSelector.SelectedIndex = 0;
            //}
            //else
            //{
            //    baseSourceSelector.SelectedIndex = 0; // pitch source
            //}
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
            volumeSlider.MouseWheel += volumeSlider_MouseWheel;

            // pan control
            panSlider = resources["panControl"] as Slider;
            panSlider.Value = Layer.Pan;
            MakeLabel("Pan", panSlider);
            panSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(panSlider_ValueChanged);
            panSlider.MouseWheel += panSlider_MouseWheel;

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

                        // redraw beat graph if necessary
                        Metronome.GetInstance().TriggerAfterBeatParsed();
                    }
                    else throw new BeatSyntaxException(errorMsg);
                }
                catch (BeatSyntaxException ex)
                {
                    new TaskDialogWrapper(Application.Current.MainWindow).Show(
                        "Beat Code Contains Error(s)",
                        "Please fix the following error(s):",
                        ex.Message,
                        TaskDialogWrapper.TaskDialogButtons.Ok,
                        TaskDialogWrapper.TaskDialogIcon.Warning);
                }
            }
        }

        protected async void baseSourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                MainWindow window = Application.Current.MainWindow as MainWindow;

                ISoundSource source = baseSourceSelector.SelectedItem as ISoundSource;

                if (source.IsPitch)
                {
                    (pitchInput.Parent as StackPanel).Visibility = Visibility.Visible;
                    // use contents of pitch field as source
                    //Layer.SetBaseSource(pitchInput.Text);

                    // disable play button while base source is being updated
                    window.playButton.IsEnabled = false;
                
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        Layer.NewBaseSource(InternalSource.GetFromPitch(pitchInput.Text));
                    });
                    window.playButton.IsEnabled = true;
                }
                else
                {
                    //sourceName = sourceName.Substring(sourceName.IndexOf('.') + 1).TrimStart();

                    //ISoundSource newSource = InternalSource.GetWavFromLabel(sourceName);

                    // set new base source
                    if (source != Layer.BaseAudioSource.SoundSource)
                    {
                        (pitchInput.Parent as StackPanel).Visibility = Visibility.Collapsed;

                        window.playButton.IsEnabled = false;
                        await window.Dispatcher.InvokeAsync(() =>
                        {
                            Layer.NewBaseSource(source);
                        });
                        window.playButton.IsEnabled = true;
                    }
                }
            }
        }

        protected void pitchInput_LostFocus(object sender, RoutedEventArgs e)
        {
            // validate input
            if (Regex.IsMatch(pitchInput.Text, @"^[A-Ga-g][#b]?\d{0,2}$|^[\d.]+$"))
            {
                string src = pitchInput.Text;
                // assume octave 4 if non given
                if (!char.IsDigit(src.Last()))
                {
                    src += '4';
                }
                Layer.NewBaseSource(InternalSource.GetFromPitch(src));
            }
        }

        protected void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Layer.Volume != volumeSlider.Value)
            {
                Layer.Volume = (float)volumeSlider.Value;
            }
        }

        /// <summary>
        /// Control the volume with mouse wheel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void volumeSlider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var slider = sender as Slider;
            double vol = Layer.Volume;
            double change = (double)e.Delta / 4800;
            vol += change;

            if (vol >= 0 && vol <= 1)
            {
                slider.Value = vol;
            }

            e.Handled = true;
        }

        protected void panSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Layer.Pan = (float)panSlider.Value;
        }

        private void panSlider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var slider = sender as Slider;
            double pan = Layer.Pan;
            double change = (double)e.Delta / 2400;
            pan += change;

            if (pan >= -1 && pan <= 1)
            {
                slider.Value = pan;
            }

            e.Handled = true;
        }

        protected void offsetInput_LostFocus(object sender, RoutedEventArgs e)
        {
            //validate
            if (BeatCell.TryParse(offsetInput.Text, out double offset))
            {
                Layer.SetOffset(offset);
                Layer.ParsedOffset = offsetInput.Text;

                Metronome.GetInstance().TriggerAfterBeatParsed();
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
            //// if no more layers, stop the playback
            //if (Metronome.GetInstance().Layers.Count == 1)
            //{
            //    (Application.Current.MainWindow as MainWindow).stopButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            //}

            // dispose the layer
            int index = Metronome.GetInstance().Layers.IndexOf(Layer);
            Layer.Dispose();
            Remove();
            // reparse layers that referenced this one
            var layers = Metronome.GetInstance().Layers.Where(x => x.ParsedString.Contains($"${index + 1}"));
            foreach(Layer layer in layers)
            {
                // check if the layer has no other cells besides the reference now pointing to itself. If so, replace the ref(s) with beat we are deleting
                if (index == Metronome.GetInstance().Layers.IndexOf(layer))
                {
                    bool hasValue = false;
                    foreach (string cell in layer.ParsedString.Split(',').Select(x => x.TrimStart(new char[] { '{', '[' })))
                    {
                        if (char.IsDigit(cell[0]))
                        {
                            hasValue = true;
                            break;
                        }
                    }
                    // replace with old beat
                    if (!hasValue)
                    {
                        // check if deleted layer has more than one cell
                        bool more = Layer.Beat.Count > 1;
                        if (more)
                        {
                            // if a single cell repeat was used on the ref, convert o multi cell
                            layer.ParsedString = Regex.Replace(
                                layer.ParsedString, 
                                $@"\${index + 1}(\(\d+\)[\d.+\-/*]*)", 
                                $"[{Layer.ParsedString}]$1");
                        }
                        // straight replace all others
                        layer.ParsedString = layer.ParsedString.Replace($"${index + 1}", Layer.ParsedString);
                        layer.UI.textEditor.Text = layer.ParsedString;
                    }
                }
                try
                {
                    layer.Parse(layer.ParsedString);
                }
                catch (Exception)
                {
                    // if there was something crazy going on, just make it "1"
                    layer.ParsedString = "1";
                    layer.Parse(layer.ParsedString);
                    layer.UI.textEditor.Text = layer.ParsedString;
                }
            }
            // redraw graph
            Metronome.GetInstance().TriggerAfterBeatParsed();
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

        /// <summary>
        /// Get the text value of the offset input.
        /// </summary>
        /// <returns></returns>
        public string GetOffsetValue()
        {
            return offsetInput.Text;
        }

        /// <summary>
        /// Set the text value of the offset input.
        /// </summary>
        /// <param name="offset"></param>
        public void SetOffsetValue(string offset)
        {
            offsetInput.Text = offset;
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
                @"@[^a-gA-G\dPpu]|"+
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
                @"^[{[]*\$s[}\]()\d+\-/*Xx]*$|"+ // empty self reference
                @"\$[\ds]+[^,|\]}(]", "Invalid beat reference."
            }
        };
    }

    class BeatSyntaxException : Exception
    {
        public BeatSyntaxException(string message) : base(message) { }
    }
}

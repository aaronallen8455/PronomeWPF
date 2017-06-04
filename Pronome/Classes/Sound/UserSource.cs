using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Pronome
{
    /// <summary>
    ///  A class for representing user defined sound sources.
    /// </summary>
    [DataContract] public class UserSource
    {
        /// <summary>
        /// Uri path to the wave file
        /// </summary>
        [DataMember]
        public string Uri { get; set; }

        /// <summary>
        /// The label string
        /// </summary>
        [DataMember]
        protected string _label;
        public string Label {
            get => _label;
            set
            {
                _label = value;
                LibraryCollectionChanged(null, null);
            }
        }

        /// <summary>
        /// 1 based index of the source
        /// </summary>
        [DataMember]
        public int Index { get; set; }

        

        [DataMember]
        public InternalSource.HiHatStatuses HiHatStatus = InternalSource.HiHatStatuses.None;

        public UserSource (string uri, string label)
        {
            Uri = uri;
            _label = label;
            // get index
            int index = 1;
            foreach (int i in Library.Select(x => x.Index).OrderBy(x => x))
            {
                if (i != index)
                {
                    break;
                }
                index++;
            }
            Index = index;
            // add to library
            Library.Insert(index - 1, this);
            //Library.Add(this);
        }

        static UserSource()
        {
            Window options = Application.Current.MainWindow.Resources["optionsWindow"] as Window;
            Library = options.Resources["userSourceLibrary"] as UserSourceLibrary;

            // subscribe to the change event of the library
            Library.CollectionChanged += LibraryCollectionChanged;
        }

        /// <summary>
        /// Apply changes to user sources in all source selector drop-downs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static void LibraryCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // apply changes to the source selectors in the layer UIs.
            List<string> sources = null;
            foreach (LayerUI ui in LayerUI.Items)
            {
                ComboBox sourceSelector = ui.baseSourceSelector;
                string current = (string)ui.baseSourceSelector.SelectedValue;

                if (sources == null)
                {
                    sources = new List<string>();

                    foreach (string s in sourceSelector.ItemsSource)
                    {
                        if (s.First() != 'u') // don't add any prexisting custom sources
                        {
                            sources.Add(s);
                        }
                    }

                    sources.AddRange(Library.OrderBy(x => x.Label).Select(x => x.ToString()));
                }

                sourceSelector.ItemsSource = sources;

                // if current source was removed, switch back to pitch
                if (!sources.Contains(current))
                {
                    sourceSelector.SelectedValue = "Pitch";
                }
            }

            // apply changes to editor
            if (sources != null && EditorWindow.Instance != default(EditorWindow))
            {
                EditorWindow.Instance.sourceSelector.ItemsSource = sources;
            }
        }

        /// <summary>
        /// A collection of all user defined sources
        /// </summary>
        static public UserSourceLibrary Library;

        /// <summary>
        /// Convert an audio file to 16000hz wave file
        /// </summary>
        /// <param name="inPath"></param>
        /// <param name="outPath"></param>
        /// <returns></returns>
        static public bool ConvertToWave16(string inPath, string outPath)
        {
            try
            {
                using (AudioFileReader reader = new AudioFileReader(inPath))
                {
                    var resampler = new WdlResamplingSampleProvider(reader, 16000);

                    WaveFileWriter.CreateWaveFile16(outPath, resampler);
                }
                return true;
            }
            catch (Exception)
            {
                // something went wrong
                return false;
            }
        }

        override public string ToString()
        {
            return $"u{Index}. {Label}";
        }
    }

    [CollectionDataContract]
    public class UserSourceLibrary : ObservableCollection<UserSource> { }

    /// <summary>
    /// A rule used for validation of source label in the options menu
    /// </summary>
    public class UserSouceLabelRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string label = (string)value;

            if (label.Length == 0)
            {
                return new ValidationResult(false, "Label cannot be empty.");
            }
            if (label.Length > 50)
            {
                return new ValidationResult(false, "Label can't be longer than 50 characters.");
            }
            if (label.First() == ' ')
            {
                return new ValidationResult(false, "Label can't start with a space.");
            }

            //UserSource.LibraryCollectionChanged(null, null);

            return new ValidationResult(true, null);
        }
    }
}

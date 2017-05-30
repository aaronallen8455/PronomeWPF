using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;

namespace Pronome
{
    /// <summary>
    ///  A class for representing user defined sound sources.
    /// </summary>
    public class UserSource
    {
        [DataMember]
        public string Uri { get; set; }

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

        [DataMember]
        public int Index { get; set; }

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

        public static void LibraryCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // apply changes to the source selectors in the layer UIs and in the editor.
            foreach (LayerUI ui in LayerUI.Items)
            {
                ComboBox sourceSelector = ui.baseSourceSelector;
                string current = (string)ui.baseSourceSelector.SelectedValue;

                List<string> sources = new List<string>();

                foreach (string s in sourceSelector.ItemsSource)
                {
                    if (s.First() != 'u')
                    {
                        sources.Add(s);
                    }
                }

                sources.AddRange(Library.Select(x => x.ToString()));

                sourceSelector.ItemsSource = sources;

                // if current source was removed, switch back to pitch
                if (!sources.Contains(current))
                {
                    sourceSelector.SelectedValue = "Pitch";
                }
            }
        }

        static public UserSourceLibrary Library;

        override public string ToString()
        {
            return $"u{Index}. {Label}";
        }
    }

    public class UserSourceLibrary : ObservableCollection<UserSource> { }

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

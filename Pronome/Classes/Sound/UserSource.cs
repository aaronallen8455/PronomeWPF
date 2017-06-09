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
using System.Windows.Data;

namespace Pronome
{
    /// <summary>
    ///  A class for representing user defined sound sources.
    /// </summary>
    [DataContract] public class UserSource : ISoundSource
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

        public bool IsPitch { get => false; }

        [DataMember]
        public InternalSource.HiHatStatuses HiHatStatus { get; set; }

        public UserSource (string uri, string label, InternalSource.HiHatStatuses hhStatus = InternalSource.HiHatStatuses.None)
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
            HiHatStatus = hhStatus;
        }

        public bool Equals(ISoundSource obj)
        {
            if (obj == null) return false;

            return Uri == obj.Uri;
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
            var sourceLibrary = Application.Current.Resources["completeSourceLibrary"] as CompleteSourceLibrary;

            sourceLibrary.OnNotifyCollectionChanged(
                new System.Collections.Specialized.NotifyCollectionChangedEventArgs( 
                    System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
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
            return $"u{Index}.".PadRight(4) + Label;
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

    /// <summary>
    /// Convert a hihat status to the index of the item in the combobox options
    /// </summary>
    [ValueConversion (typeof(InternalSource.HiHatStatuses), typeof(int))]
    public class HiHatStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            InternalSource.HiHatStatuses status = (InternalSource.HiHatStatuses)value;

            if (status == InternalSource.HiHatStatuses.None) return 0;
            if (status == InternalSource.HiHatStatuses.Closed) return 2;
            return 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if ((int)value == 0) return InternalSource.HiHatStatuses.None;
            if ((int)value == 2) return InternalSource.HiHatStatuses.Closed;
            return InternalSource.HiHatStatuses.Open;
        }
    }

    [ValueConversion (typeof(object), typeof(bool))]
    public class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return false;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}

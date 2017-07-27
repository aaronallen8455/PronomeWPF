using System.Runtime.Serialization;
using System.Windows;
using System.IO;
using System.IO.IsolatedStorage;
using System.Xml;
using System.Text;
using System.Collections.Generic;

namespace Pronome
{
    /// <summary>
    /// A class used to manage user's settings
    /// </summary>
    [DataContract]
    public class UserSettings
    {
        /// <summary>
        /// Window X position
        /// </summary>
        [DataMember]
        public double WinX;

        /// <summary>
        /// Window Y Position
        /// </summary>
        [DataMember]
        public double WinY;

        /// <summary>
        /// Window width
        /// </summary>
        [DataMember]
        public double WinWidth;

        /// <summary>
        /// Window Height
        /// </summary>
        [DataMember]
        public double WinHeight;

        /// <summary>
        /// Font size for beatcode editor
        /// </summary>
        [DataMember]
        public double BeatFontSize;

        /// <summary>
        /// Beat graph blinking toggle
        /// </summary>
        [DataMember]
        public bool BlinkingEnabled;

        /// <summary>
        /// Bounce animation queue size
        /// </summary>
        [DataMember]
        public double BounceQueueSize;

        /// <summary>
        /// Bounce animation screen division location
        /// </summary>
        [DataMember]
        public double BounceDivision;

        /// <summary>
        /// Bounce animation taper
        /// </summary>
        [DataMember]
        public double BounceWidthPad;

        /// <summary>
        /// Length of pitch decay
        /// </summary>
        [DataMember]
        public double PitchDecayLength;

        /// <summary>
        /// User's custom sources
        /// </summary>
        [DataMember]
        public UserSourceLibrary UserSourceLibrary;

        /// <summary>
        /// Whether to load the previous session on startup
        /// </summary>
        [DataMember(IsRequired = false)]
        public bool PersistSession = true;

        /// <summary>
        /// Holds the current state of the persist session toggle
        /// </summary>
        public static bool PersistSessionStatic = true;

        /// <summary>
        /// The serialized beat from the previous session.
        /// </summary>
        [DataMember(IsRequired = false)]
        public string PersistedSession;

        /// <summary>
        /// Store the settings
        /// </summary>
        public void SaveToStorage()
        {
            DataContractSerializer ds = new DataContractSerializer(typeof(UserSettings));
            using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream("pronomeSettings", FileMode.Create, isf))
                {
                    using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(isfs))
                    {
                        ds.WriteObject(writer, this);
                    }
                }
            }
        }

        /// <summary>
        /// Apply the settings
        /// </summary>
        public void ApplySettings()
        {
            Window mainWindow = Application.Current.MainWindow;

            mainWindow.Left = WinX;
            mainWindow.Top = WinY;
            mainWindow.Width = WinWidth;
            mainWindow.Height = WinHeight;
            Application.Current.Resources["textBoxFontSize"] = BeatFontSize;
            BeatGraphWindow.BlinkingIsEnabled = BlinkingEnabled;
            BounceWindow.Tick.QueueSize = BounceQueueSize;
            BounceWindow.divisionPoint = BounceDivision;
            BounceWindow.widthPad = BounceWidthPad;
            PitchStream.DecayLength = PitchDecayLength;
            UserSourceLibrary s = (mainWindow.Resources["optionsWindow"] as Window).Resources["userSourceLibrary"] as UserSourceLibrary;
            PersistSessionStatic = PersistSession;

            foreach (UserSource source in UserSourceLibrary)
            {
                s.Add(source);
            }

            // deserialize the peristed session beat if enabled
            if (PersistSession && PersistedSession != string.Empty)
            {
                DataContractSerializer ds = new DataContractSerializer(typeof(Metronome));
                byte[] bin = Encoding.UTF8.GetBytes(PersistedSession);
                using (var stream = new MemoryStream(bin))
                {
                    using (var reader = XmlDictionaryReader.CreateTextReader(stream, XmlDictionaryReaderQuotas.Max))
                    {
                        try
                        {
                            ds.ReadObject(reader);
                        
                            // need to initiate these values
                            Metronome.GetInstance().TempoChangeCued = false;
                            //Metronome.GetInstance().TempoChangedSet = new HashSet<IStreamProvider>();
                        }
                        catch (SerializationException)
                        {
                            new TaskDialogWrapper(Application.Current.MainWindow).Show(
                                "Session Persistence Failed", "An error occured while attempting to load the beat from your last session, sorry about that!",
                                "", TaskDialogWrapper.TaskDialogButtons.Ok, TaskDialogWrapper.TaskDialogIcon.Error);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the stored settings
        /// </summary>
        /// <returns></returns>
        public static UserSettings GetSettingsFromStorage()
        {
            DataContractSerializer ds = new DataContractSerializer(typeof(UserSettings));
            using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForAssembly())
            {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream("pronomeSettings", FileMode.OpenOrCreate, isf))
                {
                    using (XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(isfs, XmlDictionaryReaderQuotas.Max))
                    {
                        try
                        {
                            return (UserSettings)ds.ReadObject(reader);
                        }
                        catch (SerializationException)
                        {
                            // settings don't exist or are corrupt
                            return null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the current settings
        /// </summary>
        /// <returns></returns>
        public static UserSettings GetSettings()
        {
            Window mainWindow = MainWindow.Instance;

            bool persistSession = PersistSessionStatic;

            string serializedBeat = "";

            // stringify the current beat if it is to be persisted.
            if (persistSession)
            {
                var ds = new DataContractSerializer(typeof(Metronome));
                using (var stream = new MemoryStream())
                {
                    using (var writer = XmlDictionaryWriter.CreateTextWriter(stream, new UTF8Encoding(false)))
                    {
                        ds.WriteObject(writer, Metronome.GetInstance());
                    }
                    serializedBeat = Encoding.UTF8.GetString(stream.ToArray());
                }
            }

            return new UserSettings()
            {
                WinX = mainWindow.Left,
                WinY = mainWindow.Top,
                WinWidth = mainWindow.Width,
                WinHeight = mainWindow.Height,
                BeatFontSize = (double)Application.Current.Resources["textBoxFontSize"],
                BlinkingEnabled = BeatGraphWindow.BlinkingIsEnabled,
                BounceQueueSize = BounceWindow.Tick.QueueSize,
                BounceDivision = BounceWindow.divisionPoint,
                BounceWidthPad = BounceWindow.widthPad,
                PitchDecayLength = PitchStream.DecayLength,
                UserSourceLibrary = UserSource.Library,
                PersistSession = persistSession,
                PersistedSession = serializedBeat
            };
        }
    }
}
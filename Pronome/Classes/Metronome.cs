using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using System.Threading;
using System.Windows;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Pronome
{
    /** <summary>Has player controls, tempo, global muting options, and holds layers. Singleton</summary>
     */
    [DataContract]
    public class Metronome : IDisposable
    {
        /** <sumarry>Mix the output from all audio sources.</sumarry> */
        protected MixingSampleProvider Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(16000, 2));
        /** <summary>Access the sound output device.</summary> */
        public DirectSoundOut Player = new DirectSoundOut();

        /** <summary>The singleton instance.</summary> */
        static Metronome Instance;

        /** <summary>Used for recording to a wav file.</summary>*/
        protected WaveFileWriter Writer;

        /**<summary>Used for playing while writing to wav file.</summary>*/
        protected StreamToWavFile Recorder;

        /** <summary>A collection of all the layers.</summary> */
        [DataMember]
        public List<Layer> Layers = new List<Layer>();

        /** <summary>Constructor</summary> */
        private Metronome()
        {
            Recorder = new StreamToWavFile(Mixer);
            Player.Init(Recorder);
        }

        /** <summary>Get the singleton instance.</summary> */
        static public Metronome GetInstance()
        {
            if (Instance == null)
                Instance = new Metronome();
            return Instance;
        }

        /** <summary>Add a layer.</summary>
         * <param name="layer">Layer to add.</param> */
        public void AddLayer(Layer layer)
        {
            // add sources to mixer
            //AddSourcesFromLayer(layer);

            Layers.Add(layer);

            // transfer silent interval if exists
            if (IsSilentInterval)
            {
                foreach (IStreamProvider src in layer.AudioSources.Values)
                {
                    src.SetSilentInterval(AudibleInterval, SilentInterval);
                }

                if (layer.BasePitchSource != default(PitchStream))
                    layer.BasePitchSource.SetSilentInterval(AudibleInterval, SilentInterval);
            }
        }

        /**<summary>Used to hold a reference to the ISampleProvider so we can easily remove it from the mixer when needed.</summary>*/
        protected Dictionary<IStreamProvider, ISampleProvider> SampleDictionary = new Dictionary<IStreamProvider, ISampleProvider>();

        /** <summary>Add all the audio sources from each layer.</summary>
         * <param name="layer">Layer to add sources from.</param> */
        public void AddSourcesFromLayer(Layer layer)
        {
            // add sources to mixer
            foreach (IStreamProvider src in layer.AudioSources.Values)
            {
                if (!SampleDictionary.Keys.Contains(src))
                {
                    SampleDictionary.Add(src,
                        ((WavFileStream)src).VolumeProvider
                    );

                    Mixer.AddMixerInput(SampleDictionary[src]);
                }
            }

            if (layer.BasePitchSource != null && !SampleDictionary.Values.Contains(layer.BasePitchSource)) // if base source is a pitch stream.
            {
                Mixer.AddMixerInput(layer.BasePitchSource);
                SampleDictionary.Add(layer.BasePitchSource, layer.BasePitchSource);
            }

            // transfer silent interval if exists
            if (IsSilentInterval)
            {
                foreach (IStreamProvider src in layer.AudioSources.Values)
                {
                    src.SetSilentInterval(AudibleInterval, SilentInterval);
                }

                if (layer.BasePitchSource != default(PitchStream))
                    layer.BasePitchSource.SetSilentInterval(AudibleInterval, SilentInterval);
            }

            // need to prime the playback so that there isn't a delay the first time playing.
            if (!Recorder.IsInitialized)
            {
                Player.Play();
            }
        }

        /** <summary>Remove designated layer.</summary>
         * <param name="layer">Layer to remove.</param> */
        public void RemoveLayer(Layer layer)
        {
            Layers.Remove(layer);

            foreach (IStreamProvider src in layer.AudioSources.Values)
            {
                RemoveAudioSource(src);
            }
            if (layer.BasePitchSource != default(PitchStream))
            {
                RemoveAudioSource(layer.BasePitchSource);
            }
        }

        /**<summary>Remove an audiosource from the mixer</summary>
         * <param name="src">The IStreamProvider upcast that was originally added into the mixer</param>
         */
        public void RemoveAudioSource(IStreamProvider src)
        {
            Mixer.RemoveMixerInput(SampleDictionary[src]);
            SampleDictionary.Remove(src);
        }

        /**<summary>Add an audiosource to the mixer</summary>
         * <param name="src">The IStreamProvider from the layer's AudioSources dictionary</param>
         */
        public void AddAudioSource(IStreamProvider src)
        {
            if (src.IsPitch)
            {
                SampleDictionary.Add(src, (PitchStream)src);
                Mixer.AddMixerInput(SampleDictionary[src]);
            }
            else
            {
                SampleDictionary.Add(
                    src, 
                    ((WavFileStream)src).VolumeProvider
                );
                Mixer.AddMixerInput(SampleDictionary[src]);
            }
        }

        public enum State { Playing, Paused, Stopped };
        /**<summary>Current play state of the metronome.</summary>*/
        public State PlayState = State.Stopped;

        /** <summary>Play all layers in sync.</summary> */
        public bool Play()
        {
            if (Layers.Count > 0 && (PlayState == State.Stopped || PlayState == State.Paused))
            {
                // this timer is used to keep track of elapsed 1/4 notes so that if graph is opened, it will by synced.
                if (_timer == null)
                {
                    _timer = new AnimationTimer();
                    AnimationTimer.Start();
                }
                else
                {
                    if (PlayState == State.Stopped)
                    {
                        AnimationTimer.Init();
                        _timer.Reset();
                    }
                }

                // start playing
                Player.Play();
                PlayState = State.Playing;

                return true;
            }

            return false;
        }

        /** <summary>Stop playing and reset positions.</summary> */
        public void Stop()
        {
            if (PlayState == State.Playing || PlayState == State.Paused)
            {
                Player.Pause();

                // reset components
                foreach (Layer layer in Layers)
                {
                    layer.Reset();
                }

                Recorder.Stop();

                PlayState = State.Stopped;

                AnimationTimer.Stop(); // reset the stopwatch
                ElapsedQuarters = 0;
            }
        }

        /** <summary>Pause at current playback point.</summary> */
        public void Pause()
        {
            if (PlayState == State.Playing)
            {
                Player.Pause();

                PlayState = State.Paused;

                UpdateElapsedQuarters(); // flush the elapsed beat timer
            }
        }

        /** <summary>Playback and record to wav.</summary>
         * <param name="fileName">Name of file to record to</param>
         */
        public void Record(string fileName)
        {
            //fileName = ValidateFileName(fileName);
            Recorder.InitRecording(fileName);
            Play();
        }

        /** <summary>Record the beat to a wav file.</summary>
         * <param name="seconds">Number of seconds to record</param>
         * <param name="fileName">Name of file to record to</param>
         */
        public void ExportAsWav(double seconds, string fileName)
        {
            Writer = new WaveFileWriter(fileName, Mixer.WaveFormat);

            // if no seconds param, use the complete cycle
            if (seconds == 0)
            {
                seconds = GetQuartersForCompleteCycle() * (60d / Tempo);
            }

            int bytesToRec = (int)(Mixer.WaveFormat.AverageBytesPerSecond / 4 * seconds);
            // align bytes
            bytesToRec -= bytesToRec % 4;

            int bytesRecorded = 0;
            int cycleSize = 1280;

            while (bytesRecorded < bytesToRec)
            {
                int chunk = Math.Min(cycleSize, bytesToRec - bytesRecorded);

                float[] buffer = new float[chunk];
                Mixer.Read(buffer, 0, chunk);
                Writer.WriteSamples(buffer, 0, chunk);
                bytesRecorded += chunk;
                buffer = null;
            }

            Layers.ForEach(x => x.Reset());

            Writer.Dispose();
        }

        ///**<summary>Remove invalid characters from filename.</summary>
        // * <param name="fileName">Desired file name.</param>
        // */
        //public static string ValidateFileName(string fileName)
        //{
        //    char[] invalidChars = Path.GetInvalidFileNameChars();
        //    string invalidString = Regex.Escape(new string(invalidChars));
        //    fileName = Regex.Replace(fileName, "[" + invalidString + "]", "");
        //    return fileName;
        //}

        /**<summary>Used to accumulate elapsed quarter notes to start prior animations in sync</summary>*/
        protected AnimationTimer _timer;

        /**<summary>Number of quarter notes that have been accumulated since the beat started playing.</summary>*/
        public double ElapsedQuarters { get; protected set; }
        /**<summary>Sums up the elapsed quarter notes occuring since the last time ElapsedQuarters was run.</summary>*/
        public void UpdateElapsedQuarters()
        {
            ElapsedQuarters += _timer.GetElapsedTime() / 60 * tempo;
        }

        /**<summary>Get the quarter note value of a complete beat cycle.</summary>*/
        public double GetQuartersForCompleteCycle()
        {
            Func<double, double, double> Gcf = null;
            Gcf = delegate (double x, double y)
            {
                double r = x % y;
                if (Math.Round(r, 5) == 0) return y;

                return Gcf(y, r);
            };

            Func<double, double, double> Lcm = delegate (double x, double y)
            {
                return x * y / Gcf(x, y);
            };

            return Layers.Select(x => x.GetTotalBpmValue()).Aggregate((a, b) => Lcm(a, b));
        }

        /** <summary>The tempo in BPM.</summary> */
        protected float tempo;
        [DataMember]
        public float Tempo // in BPM
        {
            get { return tempo; }
            set { ChangeTempo(value); }
        }

        /** <summary>Change the tempo. Can be during play.</summary> */
        public void ChangeTempo(float newTempo)
        {
            float oldTempo = tempo;
            tempo = newTempo;

            if (PlayState != State.Stopped)
            {
                // add the elapsed 1/4s at current tempo
                if (PlayState == State.Playing)
                {
                    UpdateElapsedQuarters();
                }

                // modify the beat values and current byte intervals for all layers and audio sources.
                float ratio = oldTempo / newTempo;
                Layers.ForEach(x =>
                {
                    if (x.AudioSources != null)
                    {
                        x.AudioSources.Values.Select(a => {
                            //a.BeatCollection.MultiplyBeatValues(ratio);
                            a.MultiplyByteInterval(ratio);
                            return a;
                        }).ToArray();
                    }
                    if (x.BasePitchSource != null)
                    {
                        //x.BasePitchSource.BeatCollection.MultiplyBeatValues(ratio);
                        x.BasePitchSource.MultiplyByteInterval(ratio);
                    }
                });
            }
            else //(if stopped) set new tempo by recalculating all the beatCollections
            {
                Layers.ForEach(x => x.SetBeatCollectionOnSources());
            }
        }

        protected double _volume = 1;
        /** <summary>The master volume.</summary> */
        [DataMember]
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                Layers.ForEach(x => x.Volume = x.Volume); // run the 'set' function on layers
            }
        }

        /** <summary>Used for random muting.</summary> */
        protected static ThreadLocal<Random> Rand;
        /**<summary>Get random number btwn 0 and 99.</summary>*/
        public static int GetRandomNum()
        {
            if (Rand == null)
            {
                Rand = new ThreadLocal<Random>(() => new Random());
            }

            int r = Rand.Value.Next(0, 99);
            return r;
        }

        /** <summary>Is a random muting value set?</summary> */
        [DataMember]
        public bool IsRandomMute = false;

        /** <summary>Percent chance that a note gets muted.</summary> */
        [DataMember]
        public int RandomMutePercent;
        /** <summary>Number of seconds over which the random mute percent ramps up to full value.</summary> */
        [DataMember]
        public int RandomMuteSeconds = 0;

        /** <summary>Set a random mute percent.</summary>
         * <param name="percent">Percent chance for muting</param>
         * <param name="seconds">Seconds ramp til full percent muting occurs.</param> */
        public void SetRandomMute(int percent, int seconds = 0)
        {
            RandomMutePercent = percent <= 100 && percent >= 0 ? percent : 0;
            IsRandomMute = RandomMutePercent > 0 ? true : false;
            RandomMuteSeconds = seconds;

            // for wav sounds, determine if first sound should be muted, if starting at beginning.
            IEnumerable<IStreamProvider> WavLayers =
                from n in Layers
                select n.AudioSources.Values
                into s
                from aud in s
                where !aud.IsPitch
                select aud;

            foreach (WavFileStream wfs in WavLayers)
            {
                wfs.SetInitialMuting();
            }
        }

        /** <summary>True if a silent interval is set.</summary> */
        [DataMember]
        public bool IsSilentInterval = false;

        /** <summary>The value in quarter notes that a beat plays audibly.</summary> */
        [DataMember]
        public double AudibleInterval;
        /** <summary>The value in quarter notes that a beat is silenced.</summary> */
        [DataMember]
        public double SilentInterval;

        /** <summary>Set an audible/silent interval.</summary>
         * <param name="audible">The value in quarter notes that a beat plays audibly.</param>
         * <param name="silent">The value in quarter notes that a beat is silenced.</param> */
        public void SetSilentInterval(double audible, double silent)
        {
            if (audible > 0 && silent > 0)
            {
                AudibleInterval = audible;
                SilentInterval = silent;
                IsSilentInterval = true;
                // set for all audio sources
                foreach (Layer layer in Layers)
                {
                    // for each audio source in the layer
                    foreach (IStreamProvider src in layer.AudioSources.Values)
                    {
                        src.SetSilentInterval(audible, silent);
                    }

                    if (layer.BasePitchSource != default(PitchStream))
                        layer.BasePitchSource.SetSilentInterval(audible, silent);
                }
            }
            else
                IsSilentInterval = false;
        }

        /** <summary>Set an audible/silent interval.</summary>
         * <param name="audible">The value in quarter notes that a beat plays audibly.</param>
         * <param name="silent">The value in quarter notes that a beat is silenced.</param> */
        public void SetSilentInterval(string audible, string silent)
        {
            SetSilentInterval(BeatCell.Parse(audible), BeatCell.Parse(silent));
        }

        /** <summary>Save the current beat to disk.</summary>
         * <param name="name">The name for this beat.</param> */
        static public void Save(string name)
        {
            //name = ValidateFileName(name);
            var ds = new DataContractSerializer(typeof(Metronome));
            using (Stream s = File.Create(name))
            using (var w = XmlDictionaryWriter.CreateBinaryWriter(s))
            {
                ds.WriteObject(w, GetInstance());
            }
        }

        /** <summary>Load a previously saved beat by name.</summary>
         * <param name="fileName">The name of the beat to open.</param> */
        static public void Load(string fileName)
        {
            //fileName = ValidateFileName(fileName);
            var ds = new DataContractSerializer(typeof(Metronome));
            using (Stream s = File.OpenRead(fileName))
            using (var w = XmlDictionaryReader.CreateBinaryReader(s, XmlDictionaryReaderQuotas.Max))
            {
                try
                {
                    ds.ReadObject(w);
                }
                catch (SerializationException e)
                {
                    string name = Path.GetFileName(fileName);
                    MessageBox.Show($"'{name}' could not be used because it is not a valid beat file.", "Invalid Beat File", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                foreach (Layer layer in GetInstance().Layers)
                {
                    new LayerUI(MainWindow.LayerStack, layer);
                }
            }
        }

        /**<summary>Gets the contents of the saved beats directory.</summary>*/
        static public string[] GetSavedBeats()
        {
            return Directory.GetFiles("saves/").Select(x => x.Replace(".beat", "").Replace("saves/", "")).ToArray();
        }

        /**<summary>Triggered after the beat code is changed and parsed</summary>*/
        public static event EventHandler AfterBeatParsed;

        protected virtual void onAfterBeatParsed()
        {
            AfterBeatParsed?.Invoke(this, EventArgs.Empty);
        }

        public void TriggerAfterBeatParsed()
        {
            onAfterBeatParsed();
        }

        /** <summary>Prepare to deserialize. Used in loading a saved beat.</summary> */
        [OnDeserializing]
        void BeforeDeserialization(StreamingContext sc)
        {
            Instance.Dispose();
            PlayState = State.Paused;

            // remove all UI layers
            while (LayerUI.Items.Count != 0)
            {
                LayerUI.Items.First().Remove();
            }

            Instance = this;
            Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(16000, 2));
            Recorder = new StreamToWavFile(Mixer);
            Player = new DirectSoundOut();
            Player.Init(Recorder);
            SampleDictionary = new Dictionary<IStreamProvider, ISampleProvider>();
        }

        /** <summary>After deserializing, add in the layers and audio sources.</summary> */
        [OnDeserialized]
        void Deserialized(StreamingContext sc)
        {
            PlayState = State.Stopped;

            foreach (Layer layer in Layers)
            {
                layer.Deserialize();
                AddSourcesFromLayer(layer);
            }

            ChangeTempo(Tempo);

            if (IsSilentInterval)
                SetSilentInterval(AudibleInterval, SilentInterval);
            if (IsRandomMute)
                SetRandomMute(RandomMutePercent, RandomMuteSeconds);

            // trigger beat parsed
            onAfterBeatParsed();
        }

        /** <summary>Dispose of resoures from all members.</summary> */
        public void Dispose()
        {
            Player.Stop();
            Recorder.Dispose();
            Layer[] _layers = Layers.ToArray();
            for (int i=0; i<_layers.Length; i++)
            {
                _layers[i].Dispose();
            }
            Player.Dispose();
            Mixer = null;
            //Writer.Dispose();
        }
    }
}

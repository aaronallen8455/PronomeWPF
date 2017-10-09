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
        protected MixingSampleProvider Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));//WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, 44100, 2, 176400, 4, 16));
        /** <summary>Access the sound output device.</summary> */
        public WaveOut Player = new WaveOut();

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
                foreach (IStreamProvider src in layer.GetAllSources())
                {
                    src.SetSilentInterval(AudibleInterval, SilentInterval);
                }
            }
        }

        /**<summary>Used to hold a reference to the ISampleProvider so we can easily remove it from the mixer when needed.</summary>*/
        protected Dictionary<IStreamProvider, ISampleProvider> SampleDictionary = new Dictionary<IStreamProvider, ISampleProvider>();

        /// <summary>
        /// Used to coordinate a layer change with the audio render callback
        /// </summary>
        public AutoResetEvent LayerChangeTurnstile = new AutoResetEvent(false);

        /// <summary>
        /// A collection of layers that need to be changed while playing
        /// </summary>
        public Dictionary<int, Layer> LayersToChange = new Dictionary<int, Layer>();

        public double LayerChangeCycle;

        private static object layerChangeLock = new object();
        private bool? _needsToChangeLayer = false;
        public bool? NeedsToChangeLayer
        {
            get
            {
                lock (layerChangeLock)
                {
                    return _needsToChangeLayer;
                }
            }
            set
            {
                lock (layerChangeLock)
                {
                    _needsToChangeLayer = value;
                }
            }
        }

        public double ConvertBpmToSamples(double bpm, IStreamProvider src)
        {
            double result = 60 / Tempo * bpm * 44100;

            if (result > long.MaxValue) throw new Exception(bpm.ToString());

            result *= 2;

            return result;
        }

        /// <summary>
        /// Perform actions to change a layer's beat while the beat is playing.
        /// </summary>
        /// <param name="layer"></param>
        public void ExecuteLayerChange(Layer layer)
        {
            // build the dictionary
            Layer copyLayer = new Layer(
                "1", 
                layer.BaseAudioSource.SoundSource, 
                layer.ParsedOffset, 
                layer.Pan, 
                (float)layer.Volume);

            LayersToChange.Add(Layers.IndexOf(layer), copyLayer);

            copyLayer.ProcessBeatCode(layer.ParsedString);

            var t = new Thread(() =>
            {
                NeedsToChangeLayer = true;
                // wait until the cycle number is set
                LayerChangeTurnstile.WaitOne();

                FastForwardChangedLayers(LayerChangeCycle);

                // signal the audio thread to finish the process
                NeedsToChangeLayer = null;
            });
            t.Start();
        }

        /// <summary>
        /// Fast forward the layer(s) in the changed queue by the number of cycles
        /// </summary>
        /// <param name="cycles"></param>
        public void FastForwardChangedLayers(double cycles)
        {
            //double bytesPerCycle = 1;
            int floatsPerCycle = 13230;
            long totalFloats = (long)(cycles * floatsPerCycle);
            //long totalBytes = (long)(cycles * bytesPerCycle);
            // fast forward the layers
            foreach (KeyValuePair<int, Layer> pair in LayersToChange)
            {
                Layer l = pair.Value;

                foreach (IStreamProvider src in l.GetAllSources())
                {
                    long floats;
                    double offset = src.GetOffset();
                    
                    if (totalFloats > offset)
                    {
                        double layerLength = ConvertBpmToSamples(l.GetTotalBpmValue(), src);

                        double bytesToRun = (totalFloats - src.GetOffset()) % layerLength;
                        // compress the number of samples to run
                        floats = (long)(bytesToRun + offset);
                    
                        src.SampleRemainder += bytesToRun + offset - floats;
                    
                        src.IsSilentIntervalSilent(totalFloats - floats);
                    }
                    else
                    {
                        floats = totalFloats;
                    }




                    //long floats = totalFloats;
                    //
                    //long interval = (long)src.GetOffset() + 2; // block alignment
                    //if (interval < totalFloats)
                    //{
                    //    src.ProduceBytes = false;
                    //
                    //    while (interval <= floats)
                    //    {
                    //
                    //        if (src.SoundSource.IsPitch)
                    //        {
                    //            (src as PitchStream).Read(new float[interval], 0, (int)interval);
                    //        }
                    //        else
                    //        {
                    //            (src as WaveStream).Read(new byte[interval], 0, (int)interval);
                    //        }
                    //
                    //        floats -= (int)interval;
                    //        interval = src.BeatCollection.Enumerator.Current;
                    //    }
                    //
                    //    src.ProduceBytes = true;
                    //}

                    // start reading for last byteInterval
                    while (floats > 0)
                    {
                        int intsToCopy = (int)Math.Min(int.MaxValue, floats);
                        if (src.SoundSource.IsPitch)
                        {
                            (src as PitchStream).Read(new float[intsToCopy], 0, intsToCopy);
                        }
                        else
                        {
                            (src as WaveStream).Read(new byte[intsToCopy], 0, intsToCopy);
                        }
                        floats -= int.MaxValue;
                    }
                }
            }
        }

        /** <summary>Add all the audio sources from each layer.</summary>
         * <param name="layer">Layer to add sources from.</param> */
        public void AddSourcesFromLayer(Layer layer)
        {
            // add sources to mixer. Add hiHat down sounds first.
            foreach (IStreamProvider src in layer.GetAllSources().OrderBy(x => x.SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Closed))
            {
                AddAudioSource(src);
            }

            // transfer silent interval if exists
            if (IsSilentInterval)
            {
                foreach (IStreamProvider src in layer.GetAllSources())
                {
                    src.SetSilentInterval(AudibleInterval, SilentInterval);
                }
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

            foreach (IStreamProvider src in layer.GetAllSources())
            {
                RemoveAudioSource(src);
            }
        }

        /**<summary>Remove an audiosource from the mixer</summary>
         * <param name="src">The IStreamProvider upcast that was originally added into the mixer</param>
         */
        public void RemoveAudioSource(IStreamProvider src)
        {
            if (src != null && SampleDictionary.ContainsKey(src))
            {
                Mixer.RemoveMixerInput(SampleDictionary[src]);
                SampleDictionary.Remove(src);
            }
        }

        /**<summary>Add an audiosource to the mixer</summary>
         * <param name="src">The IStreamProvider from the layer's AudioSources</param>
         */
        public void AddAudioSource(IStreamProvider src)
        {
            // don't add empty sources or sources that are already added
            if (src != null && !SampleDictionary.ContainsKey(src) && src.BeatCollection.Enumerator != null)
            {
                if (src.SoundSource.IsPitch)
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
        }

        /// <summary>
        /// Enact a tempo change. Used while beat is playing.
        /// </summary>
        public void PerformTempoChanges()
        {
            foreach (Layer layer in GetInstance().Layers)
            {
                foreach (IStreamProvider src in layer.GetAllSources())
                {
                    src.MultiplyByteInterval();
                }
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
                    else if (PlayState == State.Paused)
                    {
                        AnimationTimer.Start();
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
                Player.Stop();

                // reset components
                foreach (Layer layer in Layers)
                {
                    layer.Reset();
                }

                Recorder.Stop();

                PlayState = State.Stopped;

                // reset the tempo change cycle (counts cycles in audio steams)
                MultiplyIntervalOnCycle = 0;

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
                _timer = null;
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
            Writer = new WaveFileWriter(fileName, Mixer.WaveFormat); // use CD format - or not... causes problems with pitch stream

            // if no seconds param, use the complete cycle
            if (seconds == 0)
            {
                seconds = GetQuartersForCompleteCycle() * (60d / Tempo);
            }

            int bytesToRec = (int)(Mixer.WaveFormat.AverageBytesPerSecond / 4 * seconds);
            // align bytes
            bytesToRec -= bytesToRec % 4;

            int bytesRecorded = 0;
            //FIX ME
            int cycleSize = 3520; // 44100 sample rate

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

        /**<summary>Used to accumulate elapsed quarter notes to start prior animations in sync</summary>*/
        protected AnimationTimer _timer;

        /**<summary>Number of quarter notes that have been accumulated since the beat started playing.</summary>*/
        public double ElapsedQuarters { get; protected set; }
        /**<summary>Sums up the elapsed quarter notes occuring since the last time ElapsedQuarters was run.</summary>*/
        public void UpdateElapsedQuarters()
        {
            if (_timer != null)
            {
                ElapsedQuarters += _timer.GetElapsedTime() / 60 * tempo;
            }
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

        private uint _multiplyIntervalOnCycle = 0;

        /// <summary>
        /// Used to queue when all layers change tempo so it happens in sync.
        /// </summary>
        public uint MultiplyIntervalOnCycle
        {
            get
            {
                lock (multIntervalLock)
                {
                    return _multiplyIntervalOnCycle;
                }
            }

            set
            {
                lock (multIntervalLock)
                {
                    _multiplyIntervalOnCycle = value;
                }
            }
        }
        private static object multIntervalLock = new object();

        private bool _tempoChangeCued = false;

        private static object tempoChangeLock = new object();
        public bool TempoChangeCued
        {
            get
            {
                lock (tempoChangeLock)
                {
                    return _tempoChangeCued;
                }
            }
            set
            {
                lock (tempoChangeLock)
                {
                    _tempoChangeCued = value;
                }
            }
        }

        public double TempoChangeRatio;

        /** <summary>Change the tempo. Can be during play.</summary> */
        public void ChangeTempo(float newTempo)
        {
            // add the elapsed 1/4s at current tempo
            if (PlayState == State.Playing)
            {
                UpdateElapsedQuarters();
            }

            float oldTempo = tempo;
            tempo = newTempo;

            if (PlayState != State.Stopped)
            {

                // modify the beat values and current byte intervals for all layers and audio sources.
                double ratio = oldTempo / newTempo;
                TempoChangeRatio = ratio;
                TempoChangeCued = true;
            }
            else //(if stopped) set new tempo by recalculating all the beatCollections
            {
                foreach (Layer layer in Layers)
                {
                    layer.Beat = layer.SetBeatCollectionOnSources(layer.Beat.ToArray()).ToList();
                    layer.ResetSources();
                }
                //Layers.ForEach(x => x.SetBeatCollectionOnSources());
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
        public bool IsRandomMute = false;

        /** <summary>Percent chance that a note gets muted.</summary> */
        public int RandomMutePercent;

        /** <summary>Number of seconds over which the random mute percent ramps up to full value.</summary> */
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
                where !aud.SoundSource.IsPitch
                select aud;

            foreach (WavFileStream wfs in WavLayers)
            {
                wfs.SetInitialMuting();
            }
        }

        /** <summary>True if a silent interval is set.</summary> */
        public bool IsSilentInterval = false;

        /** <summary>The value in quarter notes that a beat plays audibly.</summary> */
        public double AudibleInterval;

        /** <summary>The value in quarter notes that a beat is silenced.</summary> */
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

                    // need to initiate these values
                    GetInstance().TempoChangeCued = false;
                    //GetInstance().TempoChangedSet = new HashSet<IStreamProvider>();
                    //GetInstance().TempoChangeCounter = 0;
                }
                catch (SerializationException)
                {
                    string name = Path.GetFileName(fileName);
                    new TaskDialogWrapper(Application.Current.MainWindow).Show(
                        "Invalid Beat File", $"'{name}' could not be used because it is not a valid beat file.", 
                        "", TaskDialogWrapper.TaskDialogButtons.Ok, TaskDialogWrapper.TaskDialogIcon.Error);
                    //MessageBox.Show($"'{name}' could not be used because it is not a valid beat file.", "Invalid Beat File", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            Recorder = new StreamToWavFile(Mixer);
            Player = new WaveOut();
            Player.Init(Recorder);
            SampleDictionary = new Dictionary<IStreamProvider, ISampleProvider>();
        }

        /** <summary>After deserializing, add in the layers and audio sources.</summary> */
        [OnDeserialized]
        void Deserialized(StreamingContext sc)
        {
            PlayState = State.Stopped;

            // init layer change objects
            NeedsToChangeLayer = false;
            LayersToChange = new Dictionary<int, Layer>();
            LayerChangeTurnstile = new AutoResetEvent(false);

            foreach (Layer layer in Layers)
            {
                layer.Deserialize();
                AddSourcesFromLayer(layer);
            }

            ChangeTempo(Tempo);

            //if (IsSilentInterval)
            //    SetSilentInterval(AudibleInterval, SilentInterval);
            //if (IsRandomMute)
            //    SetRandomMute(RandomMutePercent, RandomMuteSeconds);

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

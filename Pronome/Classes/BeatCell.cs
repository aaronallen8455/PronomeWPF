using System;
using System.Linq;

namespace Pronome
{
    /**<summary>Contains the timing information for each individual beat cell.</summary>*/
    public class BeatCell
    {
        /**<summary>The time length in number of bytes.</summary>*/
        public double ByteInterval;

        /**<summary>The time length in quarter notes</summary>*/
        public double Bpm; // value expressed in BPM time.

        /**<summary>The name of the audio source. Could be a wav file or a pitch.</summary>*/
        public string SourceName;

        /**<summary>The layer that this cell belongs to.</summary>*/
        public Layer Layer;

        /**<summary>The audio source used for this cell.</summary>*/
        public IStreamProvider AudioSource;

        /**<summary>True for HiHat pedal down sounds.</summary>*/
        public bool IsHiHatClosed = false;

        /**<summary>True for HiHat open sounds.</summary>*/
        public bool IsHiHatOpen = false;

        /**<summary>If using hihat sounds, how long in BPM the open hihat sound should last.</summary>*/
        public double hhDuration = 0;

        /**<summary>Sets the 'byteinterval' based on current tempo and audiosource sample rate.</summary>*/
        public void SetBeatValue()
        {
            // set byte interval based on tempo and audiosource sample rate
            ByteInterval = ConvertFromBpm(Bpm, AudioSource);
        }

        /**<summary>Convert a quarter note time value into a byte count.</summary>
         * <param name="bpm">Number of quarter-notes.</param>
         * <param name="src">The audio source to get the bytes/second from.</param>
         */
        static public double ConvertFromBpm(double bpm, IStreamProvider src)
        {
            double result = bpm * (60d / Metronome.GetInstance().Tempo) * src.WaveFormat.SampleRate;

            if (result > long.MaxValue) throw new Exception(bpm.ToString());

            return result;
        }

        /**<summary>Constructor</summary>
         * <param name="beat">The numeric expression to be parsed.</param>
         * <param name="sourceName">The name of the audio source used by this cell.</param>
         */
        public BeatCell(string beat, string sourceName = "")
        {
            SourceName = sourceName;
            Bpm = Parse(beat);

            // is it a hihat closed or open sound?
            if (HiHatOpenFileNames.Contains(sourceName))
            {
                IsHiHatOpen = true;
            }
            else if (HiHatClosedFileNames.Contains(sourceName))
            {
                IsHiHatClosed = true;
            }
        }

        /**<summary>A list of the HiHat open sound file names.</summary>*/
        static public string[] HiHatOpenFileNames = new string[]
        {
            "Pronome.wav.hihat_half_center_v4.wav",
            "Pronome.wav.hihat_half_center_v7.wav",
            "Pronome.wav.hihat_half_center_v10.wav",
            "Pronome.wav.hihat_half_edge_v7.wav",
            "Pronome.wav.hihat_half_edge_v10.wav",
            "Pronome.wav.hihat_open_center_v4.wav",
            "Pronome.wav.hihat_open_center_v7.wav",
            "Pronome.wav.hihat_open_center_v10.wav",
            "Pronome.wav.hihat_open_edge_v7.wav",
            "Pronome.wav.hihat_open_edge_v10.wav"
        };

        /**<summary>A list of the HiHat closed sound file names.</summary>*/
        static public string[] HiHatClosedFileNames = new string[]
        {
            "Pronome.wav.hihat_pedal_v3.wav",
            "Pronome.wav.hihat_pedal_v5.wav"
        };

        /**<summary>Parse a math expression string.</summary>
         * <param name="str">String to parse.</param>
         */
        static public double Parse(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;

            string operators = "";
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '+' || str[i] == '-' || str[i] == '*' || str[i] == '/')
                    operators += str[i];
            }
            double[] numbers = str.Split(new char[] { '+', '-', '*', '/' }).Select((x) => Convert.ToDouble(x)).ToArray();

            // do mult and div
            while (operators.IndexOfAny(new[] { '*', '/' }) > -1)
            {
                int index = operators.IndexOfAny(new[] { '*', '/' });

                switch (operators[index])
                {
                    case '*':
                        numbers[index] *= numbers[index + 1];
                        numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
                        break;
                    case '/':
                        numbers[index] /= numbers[index + 1];
                        numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
                        break;
                }
                operators = operators.Remove(index, 1);
            }
            // do addition and subtraction
            while (operators.IndexOfAny(new[] { '+', '-' }) > -1)
            {
                int index = operators.IndexOfAny(new[] { '+', '-' });

                switch (operators[index])
                {
                    case '+':
                        numbers[index] += numbers[index + 1];
                        numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
                        break;
                    case '-':
                        numbers[index] -= numbers[index + 1];
                        numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
                        break;
                }
                operators = operators.Remove(index, 1);
            }

            if (numbers[0] > long.MaxValue) throw new Exception(numbers[0].ToString());

            return numbers[0];
        }

        /// <summary>
        /// Parse a math expression after testing validity
        /// </summary>
        /// <param name="str">String to parse</param>
        /// <param name="val">Output value</param>
        /// <returns></returns>
        static public bool TryParse(string str, out double val)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(str, @"(\d+\.?\d*[\-+*/xX]?)*\d+\.?\d*$"))
            {
                val = Parse(str);

                return true;
            }
            val = 0;
            return false;
        }
    }
}

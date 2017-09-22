using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

namespace Pronome
{
    /**<summary>Contains the timing information for each individual beat cell.</summary>*/
    public class BeatCell
    {
        //**<summary>The time length in number of bytes.</summary>*/
        //public double ByteInterval;

        /**<summary>The time length in quarter notes</summary>*/
        public double Bpm; // value expressed in BPM time.

        ///**<summary>Holds info about the sound source.</summary>*/
        public ISoundSource SoundSource;

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

        ///**<summary>Sets the 'byteinterval' based on current tempo and audiosource sample rate.</summary>*/
        //public void SetBeatValue()
        //{
        //    // set byte interval based on tempo and audiosource sample rate
        //    //ByteInterval = ConvertFromBpm(Bpm, AudioSource);
        //}

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
        public BeatCell(string beat, Layer layer, ISoundSource source = null)
        {
            Layer = layer;

            SoundSource = source;
            Bpm = Parse(beat);

            // is it a hihat closed or open sound? use layer source if null
            if (source == null)
            {
                IsHiHatOpen = Layer.BaseAudioSource.SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open;
                IsHiHatClosed = Layer.BaseAudioSource.SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Closed;
            }
            else
            {
                IsHiHatOpen = source.HiHatStatus == InternalSource.HiHatStatuses.Open;
                IsHiHatClosed = source.HiHatStatus == InternalSource.HiHatStatuses.Closed;
            }
            
        }

        ///**<summary>A list of the HiHat open sound file names.</summary>*/
        //static public string[] HiHatOpenFileNames = new string[]
        //{
        //    "Pronome.wav.hihat_half_center_v4.wav",
        //    "Pronome.wav.hihat_half_center_v7.wav",
        //    "Pronome.wav.hihat_half_center_v10.wav",
        //    "Pronome.wav.hihat_half_edge_v7.wav",
        //    "Pronome.wav.hihat_half_edge_v10.wav",
        //    "Pronome.wav.hihat_open_center_v4.wav",
        //    "Pronome.wav.hihat_open_center_v7.wav",
        //    "Pronome.wav.hihat_open_center_v10.wav",
        //    "Pronome.wav.hihat_open_edge_v7.wav",
        //    "Pronome.wav.hihat_open_edge_v10.wav"
        //};

        ///**<summary>A list of the HiHat closed sound file names.</summary>*/
        //static public string[] HiHatClosedFileNames = new string[]
        //{
        //    "Pronome.wav.hihat_pedal_v3.wav",
        //    "Pronome.wav.hihat_pedal_v5.wav"
        //};

        /**<summary>Parse a math expression string.</summary>
         * <param name="str">String to parse.</param>
         */
        static public double Parse(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;

            // holds value that will be added to current value after all mult and div have occurred
            double accum = 0;
            // builds up a numeric value from individual chars
            StringBuilder curNum = new StringBuilder("0");
            // the most recently converted (from chars) number and preceding mult/div operations
            double focusedNum = 0;
            char curOp = ' ';
            // true if accum will be added to focusedNum
            bool willAdd = true;
            foreach (char c in str)
            {
                if (char.IsNumber(c) || c == '.')
                {
                    curNum.Append(c);
                }
                else
                {
                    double num = double.Parse(curNum.ToString());

                    // perform * / on focused num
                    if (curOp == '*')
                    {
                        focusedNum *= num;
                    }
                    else if (curOp == '/')
                    {
                        focusedNum /= num;
                    }
                    else
                    {
                        // last op was + or -
                        focusedNum = num;
                    }

                    curNum.Clear();

                    switch (c)
                    {
                        case '-':
                        case '+':
                            // we can now apply the waiting +/- operation
                            if (willAdd)
                            {
                                accum += focusedNum;
                            }
                            else
                            {
                                accum -= focusedNum;
                            }

                            willAdd = c == '+';
                            curOp = c;
                            focusedNum = accum;
                            break;
                        case 'X':
                        case 'x':
                        case '*':
                            curOp = '*';
                            break;
                        case '/':
                            curOp = '/';
                            break;
                    }
                }
            }

            // tie up the loose ends

            if (curOp == ' ')
            {
                return double.Parse(str);
            }

            double lastNum = double.Parse(curNum.ToString());

            switch (curOp)
            {
                case '+':
                    accum += lastNum;
                    break;
                case '-':
                    accum -= lastNum;
                    break;
                case '*':
                    focusedNum *= lastNum;

                    if (willAdd)
                    {
                        accum += focusedNum;
                    }
                    else
                    {
                        accum -= focusedNum;
                    }
                    break;
                case '/':
                    focusedNum /= lastNum;

                    if (willAdd)
                    {
                        accum += focusedNum;
                    }
                    else
                    {
                        accum -= focusedNum;
                    }
                    break;
            }

            return accum;
        }

        /// <summary>
        /// Validate a beat code expression.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static public bool ValidateExpression(string str)
        {
            return Regex.IsMatch(str, @"(\d+\.?\d*[\-+*/xX]?)*\d+\.?\d*$");
        }

        /// <summary>
        /// Parse a math expression after testing validity
        /// </summary>
        /// <param name="str">String to parse</param>
        /// <param name="val">Output value</param>
        /// <returns></returns>
        static public bool TryParse(string str, out double val)
        {
            if (ValidateExpression(str))
            {
                val = Parse(str);

                return true;
            }
            val = 0;
            return false;
        }

        /// <summary>
        /// Simplify an expression so that fractions/decimals are combined.
        /// </summary>
        /// <param name="value">Ex. 1/3+1/6 = 1/2</param>
        /// <returns></returns>
        public static string SimplifyValue(string value)
        {
            value = value.Replace('x', '*').Replace('X', '*'); // replace multiply symbol

            // merge all fractions based on least common denominator
            int lcd = 1;

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

            //// replace common decimals with fraction if other fractions coexist in the string
            //if (value.IndexOf('/') > -1)
            //{
            //    value = ConvertCommonFractions(value);
            //}

            // simplify all fractions
            var multDiv = Regex.Matches(value, @"(?<!\.\d*)(\d+?[*/](?=\d+))+\d+(?!\d*\.)"); // get just the fractions or whole number multiplication. filter out decimals
            foreach (Match m in multDiv)
            {
                int n = 1; // numerator
                int d = 1; // denominator
                foreach (Match num in Regex.Matches(m.Value, @"(?<=\*)\d+"))
                {
                    n *= int.Parse(num.Value);
                }
                n *= int.Parse(Regex.Match(m.Value, @"\d+").Value);
                foreach (Match num in Regex.Matches(m.Value, @"(?<=/)\d+"))
                {
                    d *= int.Parse(num.Value);
                }

                int gcf = (int)Gcf(n, d);
                n /= gcf;
                d /= gcf;
                // replace with simplified fraction
                int index = value.IndexOf(m.Value);
                value = value.Substring(0, index) + n.ToString() + '/' + d.ToString() + value.Substring(index + m.Length);
            }

            var denoms = Regex.Matches(value, @"(?<=/)\d+(?!\d*\.)");

            foreach (Match m in denoms)
            {
                int d = int.Parse(m.Value);
                if (lcd == 1)
                {
                    lcd = d;
                }
                else
                {
                    lcd = (int)Lcm(lcd, d);
                }
            }

            // aggregate the fractions
            var fracs = Regex.Matches(value, @"(?<!\.\d*)(\+|-|^)(\d+/\d+)(?!\d*\.)");
            int numerator = 0;
            foreach (Match m in fracs)
            {
                int[] fraction = Array.ConvertAll(m.Groups[2].Value.Split('/'), Convert.ToInt32);
                int num = fraction[0]; // numerator
                int den = fraction[1]; // denominator

                int ratio = lcd / den;
                string sign = m.Groups[1].Value;
                numerator += num * ratio * (sign == "-" ? -1 : 1);

                // remove all individual fraction to be replaced by 1 big fraction
                int index = value.IndexOf(m.Value);
                value = value.Remove(index, m.Length);
            }
            //value = numerator.ToString() + '/' + lcd.ToString() + value;
            int whole = numerator / lcd;
            numerator -= whole * lcd;
            // if the fraction is negative, subtract it from the whole number
            if (numerator < 0)
            {
                numerator += lcd;
                whole--;
            }

            if (lcd > 1)
            {
                // simplify the fraction
                int gcf = (int)Gcf(numerator, lcd);
                numerator /= gcf;
                lcd /= gcf;
            }

            string fractionPart = numerator.ToString() + '/' + lcd.ToString();

            // merge all whole numbers and decimals
            double numbers = whole + Parse("0+0" + value);// 0;

            string result = numbers != 0 ? numbers.ToString().TrimStart('0') : "";

            bool recurse = false;
            // append the fractional portion of it's not zero
            if (numerator != 0)
            {
                // check if 'numbers' has some decimal that can become a fraction. If so, we'll recurse
                string r = ConvertCommonFractions(result);
                if (!string.IsNullOrEmpty(r))
                {
                    recurse = true;
                    result = r;
                }

                // put the positive value first.
                if (numbers < 0)
                {
                    result = fractionPart + result; // if both are negative, something went horribly wrong.
                }
                else
                {
                    if (numbers != 0)
                    {
                        result += fractionPart[0] != '-' ? "+" : "";
                    }
                    result += fractionPart;
                }
            }

            // if there's more than one fraction, recurse
            if (recurse)
            {
                return SimplifyValue(result);
            }

            return result;
        }

        /// <summary>
        /// Replace decimals with corresponding fractions if common.
        /// </summary>
        /// <param name="input"></param>
        /// <returns>String or Null</returns>
        static protected string ConvertCommonFractions(string input)
        {
            bool changed = false;

            foreach (Match m in Regex.Matches(input, @"(^|\+|-)(\d*)\.(\d+)(?=$|\+|-)"))
            {
                string sign = m.Groups[1].Value;
                string bridge = sign == "" ? "+" : sign;
                string wholeNum = m.Groups[2].Value;
                string fract = m.Groups[3].Value;
                // switch out decimal with fraction if matches
                bool switched = true;
                switch (fract)
                {
                    case "5":
                        fract = "1/2";
                        break;
                    case "25":
                        fract = "1/4";
                        break;
                    case "75":
                        fract = "3/4";
                        break;
                    default:
                        switched = false;
                        break;
                }

                if (switched)
                {
                    changed = true;
                    StringBuilder replace = new StringBuilder();
                    replace.Append(sign);
                    if (!string.IsNullOrEmpty(wholeNum))
                    {
                        replace.Append(wholeNum).Append(bridge);
                    }
                    replace.Append(fract);
                    // replace
                    int index = input.IndexOf(m.Value);
                    input = input.Substring(0, index) + replace.ToString() + input.Substring(index + m.Length);
                }
            }

            if (changed)
            {
                return input;
            }

            return null; // no change
        }

        /// <summary>
        /// Multiply all terms in an expression by the factor
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="factor"></param>
        /// <returns></returns>
        static public string MultiplyTerms(string exp, double factor)
        {
            if (string.IsNullOrEmpty(exp)) return "";

            string[] terms = Regex.Split(exp, @"(?<!^)(?=[+\-])");
            // multiply each seperate term by the factor
            for (int i = 0; i < terms.Length; i++)
            {
                terms[i] += '*' + factor.ToString();
            }

            return string.Join(string.Empty, terms);
        }

        /// <summary>
        /// Subtract the right term from the left.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        static public string Subtract(string left, string right)
        {
            //StringBuilder sb = new StringBuilder(right);
            //for (int i = 0; i < right.Length; i++)
            //{
            //    if (right[i] == '+') sb[i] = '-';
            //    else if (right[i] == '-') sb[i] = '+';
            //}
            right = Invert(right);

            return Add(left, right);
        }

        static public string Add(string left, string right)
        {
            return SimplifyValue($"{left}+0{right}");
        }

        /// <summary>
        /// Invert an expression
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        static public string Invert(string exp)
        {
            if (string.IsNullOrEmpty(exp))
            {
                return "";
            }

            char[] ops = new char[] { '+', '-', '/', '*' };
            StringBuilder sb = new StringBuilder(exp);

            if (exp[0] == '-')
            {
                sb.Remove(0, 1);
            }
            else
            {
                sb.Insert(0, '-');
            }

            for (int i = 1; i < sb.Length; i++)
            {
                if (sb[i] == '+') sb[i] = '-';
                else if (sb[i] == '-') {
                    if (ops.Contains(sb[i - 1]))
                    {
                        sb.Remove(i, 1);
                        i--;
                    }
                    else
                    {
                        sb[i] = '+';
                    }
                }
                //else if (sb[i-1] == '*')
                //{
                //    // If dividing/multiplying by a positive number, make it negative
                //    sb.Insert(i, '-');
                //    i++;
                //}
            }

            return sb.ToString();
        }
    }
}

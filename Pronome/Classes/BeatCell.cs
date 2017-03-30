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

            StringBuilder ops = new StringBuilder();
            string operators = "";
            StringBuilder number = new StringBuilder();
            List<double> numbers = new List<double>();
            // parse out the numbers and operators
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '+' || str[i] == '*' || str[i] == '/' || (str[i] == '-' && (str[i-1] != '*' || str[i-1] != '/')))
                {
                    numbers.Add(double.Parse(number.ToString()));
                    number.Clear();
                    ops.Append(str[i]);
                }
                else if (str[i] == 'x' || str[i] == 'X')
                {
                    ops.Append('*');
                }
                else
                {
                    number.Append(str[i]);
                }
            }
            numbers.Add(double.Parse(number.ToString()));
            operators = ops.ToString();
            //double[] numbers = str.Split(new char[] { '+', '-', '*', '/' }).Select((x) => Convert.ToDouble(x)).ToArray();

            double result = 0;
            double current = numbers[0];
            char connector = '+';
            // perform arithmetic
            for (int i = 0; i < operators.Length; i++)
            {
                char op = operators[i];
                if (op == '*')
                {
                    current *= numbers[i + 1];
                }
                else if (op == '/')
                {
                    current /= numbers[i + 1];
                }
                else
                {
                    if (connector == '+')
                    {
                        result += current;
                    }
                    else if (connector == '-')
                    {
                        result -= current;
                    }
                    //result += current;
                    if (i < operators.Length - 1)
                    {
                        
                        if (operators[i + 1] == '+' || operators[i + 1] == '-')
                        {
                            current = 0;

                            if (op == '+')
                            {
                                result += numbers[i + 1];
                            }
                            else
                            {
                                result -= numbers[i + 1];
                            }
                        }
                        else
                        {
                            connector = op;
                            current = numbers[i + 1];
                        }
                    }
                    else
                    {
                        current = 0;

                        if (op == '+')
                        {
                            result += numbers[i + 1];
                        }
                        else
                        {
                            result -= numbers[i + 1];
                        }
                    }
                }
            }
            result = connector == '+' ? result + current : result - current;

            return result;

            //// do mult and div
            //while (operators.IndexOfAny(new[] { '*', '/' }) > -1)
            //{
            //    int index = operators.IndexOfAny(new[] { '*', '/' });
            //
            //    switch (operators[index])
            //    {
            //        case '*':
            //            numbers[index] *= numbers[index + 1];
            //            numbers.Remove(index + 1);
            //            //numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
            //            break;
            //        case '/':
            //            numbers[index] /= numbers[index + 1];
            //            numbers.Remove(index + 1);
            //            //numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
            //            break;
            //    }
            //    operators = operators.Remove(index, 1);
            //}
            //// do addition and subtraction
            //while (operators.IndexOfAny(new[] { '+', '-' }) > -1)
            //{
            //    int index = operators.IndexOfAny(new[] { '+', '-' });
            //
            //    switch (operators[index])
            //    {
            //        case '+':
            //            numbers[index] += numbers[index + 1];
            //            numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
            //            break;
            //        case '-':
            //            numbers[index] -= numbers[index + 1];
            //            numbers = numbers.Take(index + 1).Concat(numbers.Skip(index + 2)).ToArray();
            //            break;
            //    }
            //    operators = operators.Remove(index, 1);
            //}
            //
            //if (numbers[0] > long.MaxValue) throw new Exception(numbers[0].ToString());
            //
            //return numbers[0];
        }

        /// <summary>
        /// Parse a math expression after testing validity
        /// </summary>
        /// <param name="str">String to parse</param>
        /// <param name="val">Output value</param>
        /// <returns></returns>
        static public bool TryParse(string str, out double val)
        {
            if (Regex.IsMatch(str, @"(\d+\.?\d*[\-+*/xX]?)*\d+\.?\d*$"))
            {
                val = Parse(str);

                return true;
            }
            val = 0;
            return false;
        }

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

            // simplify all fractions
            var multDiv = Regex.Matches(value, @"(?<!\.)(\d+[*/](?=\d+))+\d+(?!\.)");
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

            var denoms = Regex.Matches(value, @"(?<=/)\d+(?!\.)");

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
            var fracs = Regex.Matches(value, @"(?<!\.)(\+|-|^)(\d+/\d+)(?!\.)");
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
            string fractionPart = numerator.ToString() + '/' + lcd.ToString();

            // merge all whole numbers and decimals
            double numbers = whole + Parse("0+0" + value);// 0;
            //if (!string.IsNullOrEmpty(value))
            //{
            //    numbers = whole + Parse("0+0" + value);
            //}

            string result = numbers != 0 ? numbers.ToString() : "";
            // append the fractional portion of it's not zero
            if (numerator != 0)
            {
                if (numbers != 0)
                {
                    result += fractionPart[0] != '-' ? "+" : "";
                }
                result += fractionPart;
            }

            return result;
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

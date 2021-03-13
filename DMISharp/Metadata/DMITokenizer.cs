using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Toolkit.HighPerformance.Enumerables;
using Microsoft.Toolkit.HighPerformance.Extensions;

namespace DMISharp.Metadata
{
    /// <summary>
    /// Ref struct for tokenizing a DMI's metadata text into key and value pairs using ReadOnlySpans
    /// </summary>
    public ref struct DMITokenizer
    {
        private ReadOnlySpanTokenizer<char> _tokenizer;
        private ReadOnlySpan<char> _key;
        private ReadOnlySpan<char> _value;

        /// <summary>
        /// Instantiate a new DMITokenizer to tokenize a provided ReadOnlySpan
        /// </summary>
        /// <param name="data">The span to tokenize into key, value pairs</param>
        public DMITokenizer(ReadOnlySpan<char> data)
        {
            _tokenizer = data.Tokenize('\n');
            _key = ReadOnlySpan<char>.Empty;
            _value = ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Attempts to get the next key, value pair.
        /// </summary>
        /// <returns>True if an additional pair is found, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_tokenizer.MoveNext())
            {
                var line = _tokenizer.Current;
                
#if NETSTANDARD || NET472 || NET461
                // Skip comments
                if (line.StartsWith("#".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    continue;

                // Strip any whitespace
                var trimmedLine = line.Trim("\n\t ".AsSpan());
                
                // Skip any lines without assignment
                var equalsIndex = trimmedLine.IndexOf("=".AsSpan(), StringComparison.OrdinalIgnoreCase);
                if (equalsIndex == -1)
                    continue;
                
                // Extract key and value
                _key = trimmedLine[..(equalsIndex - 1)].TrimEnd(" ".AsSpan());
                _value = trimmedLine[(equalsIndex + 1)..].Trim(" ".AsSpan());
                
                // Trip any wrapping quotes if value is a string
                _value = _value.Trim("\"".AsSpan());
#else
                // Skip comments
                if (line.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Strip any whitespace
                var trimmedLine = line.Trim("\n\t ");
                
                // Skip any lines without assignment
                var equalsIndex = trimmedLine.IndexOf("=", StringComparison.OrdinalIgnoreCase);
                if (equalsIndex == -1)
                    continue;
                
                // Extract key and value
                _key = trimmedLine[..(equalsIndex - 1)].TrimEnd(" ");
                _value = trimmedLine[(equalsIndex + 1)..].Trim(" ");
                
                // Trip any wrapping quotes if value is a string
                _value = _value.Trim("\"");
#endif
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// The current token key
        /// </summary>
        public ReadOnlySpan<char> CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this._key;
        }

        /// <summary>
        /// The current token value
        /// </summary>
        public ReadOnlySpan<char> CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this._value;
        }

        /// <summary>
        /// Determines if the current key token is equal to some value
        /// </summary>
        /// <param name="value">The value to compare against</param>
        /// <returns>If the two values are equal</returns>
        public bool KeyEquals(ReadOnlySpan<char> value)
        {
            return CurrentKey.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
        
#if NETSTANDARD || NET472 || NET461
        /// <summary>
        /// Determines if the current key token is equal to some value
        /// </summary>
        /// <param name="value">The value to compare against</param>
        /// <returns>If the two values are equal</returns>
        public bool KeyEquals(string value)
        {
            return KeyEquals(value.AsSpan());
        }
#endif

        /// <summary>
        /// Attempts to return the current value token as an integer.
        /// </summary>
        /// <returns>The current value token as an integer</returns>
        public int ValueAsInt()
        {
#if NETSTANDARD || NET472 || NET461
            var currentValue = CurrentValue.ToString();
#else
            var currentValue = CurrentValue;
#endif
            
            try
            {
                return int.Parse(currentValue, provider: CultureInfo.InvariantCulture);
            }
            catch (FormatException e)
            {
                throw new FormatException($"Failed to parse int {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"", e);
            }
        }

        /// <summary>
        /// Attempts to return the current value token as a double.
        /// </summary>
        /// <returns>The current value token as a double</returns>
        public double ValueAsDouble()
        {
#if NETSTANDARD || NET472 || NET461
            var currentValue = CurrentValue.ToString();
#else
            var currentValue = CurrentValue;
#endif

            try
            {
                return double.Parse(currentValue, provider: CultureInfo.InvariantCulture);
            }
            catch (FormatException e)
            {
                throw new FormatException($"Failed to parse double {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"", e);
            }
        }

        /// <summary>
        /// Attempts to return the current value token as a bool.
        /// </summary>
        /// <returns>The current value token as a bool</returns>
        public bool ValueAsBool()
        {
            return ValueAsInt() == 1;
        }
        
        /// <summary>
        /// Attempts to return the current value token as an array of doubles.
        /// </summary>
        /// <returns>The current value token as an array of doubles</returns>
        public double[] ValueAsDoubleArray()
        {
            try
            {
                return CurrentValue.ToString().Split(',')
                    .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            }
            catch (FormatException e)
            {
                throw new FormatException($"Failed to parse double array {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"", e);
            }
        }
        
        /// <summary>
        /// Attempts to return the current value token as an array of ints.
        /// </summary>
        /// <returns>The current value token as an array of ints</returns>
        public int[] ValueAsIntArray()
        {
            try
            {
                return CurrentValue.ToString().Split(',')
                    .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            }
            catch (FormatException e)
            {
                throw new FormatException($"Failed to parse int array {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"", e);
            }
        }
    }
}
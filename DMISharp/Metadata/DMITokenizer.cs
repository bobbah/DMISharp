using System;
using System.Runtime.CompilerServices;
using Microsoft.Toolkit.HighPerformance.Enumerables;
using Microsoft.Toolkit.HighPerformance.Extensions;

namespace DMISharp.Metadata
{
    public ref struct DMITokenizer
    {
        private ReadOnlySpanTokenizer<char> _tokenizer;
        private ReadOnlySpan<char> _key;
        private ReadOnlySpan<char> _value;

        public DMITokenizer(ReadOnlySpan<char> data)
        {
            _tokenizer = data.Tokenize('\n');
            _key = ReadOnlySpan<char>.Empty;
            _value = ReadOnlySpan<char>.Empty;
        }

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
        
        public ReadOnlySpan<char> CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this._key;
        }

        public ReadOnlySpan<char> CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this._value;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CustomOnChipDebuggerConsoleApp
{
    public class GDBPacket
    {
        private readonly byte[] _message;
        private readonly int _length;
        internal string _text;

        private char _commandName;
        private readonly string[] _parameters;

        private static readonly Regex RemovePrefix = new Regex(@"^[\+\$]+", RegexOptions.Compiled);

        public GDBPacket(byte[] message, int length)
        {
            _message = message;
            _length = length;

            var encoder = new ASCIIEncoding();
            _text = encoder.GetString(message, 0, length);

            var request = RemovePrefix.Replace(_text, "");
            if (String.IsNullOrEmpty(request))
            {
                _commandName = '\0';
            }
            else
            {
                _commandName = request[0];
                _parameters = request.Substring(1).Split(new char[] { ',', '#', ':', ';' });
            }
        }

        public override string ToString()
        {
            return _text;
        }

        public byte[] GetBytes()
        {
            return _message;
        }

        public int Length
        {
            get { return _length; }
        }

        public char CommandName
        {
            get { return _commandName; }
        }

        public string[] GetCommandParameters()
        {
            return _parameters;
        }

        public static string CalculateCRC(string str)
        {
            var encoder = new ASCIIEncoding();
            var bytes = encoder.GetBytes(str);
            var crc = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                crc += bytes[i];
            }
            return ((byte)crc).ToLowEndianHexString().ToLowerInvariant();
        }
    }
}

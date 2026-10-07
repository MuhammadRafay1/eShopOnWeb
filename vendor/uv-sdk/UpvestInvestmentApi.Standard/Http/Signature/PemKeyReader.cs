// <copyright file="PemKeyReader.cs" company="Upvest">
// UpvestInvestmentApi.Standard
// </copyright>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UpvestInvestmentApi.Standard.Http.Signature
{
    /// <summary>
    /// Loads an EC private key from PEM text into an <see cref="ECDsa"/> instance.
    /// </summary>
    /// <remarks>
    /// Supports the three shapes OpenSSL emits for EC keys:
    /// <list type="bullet">
    /// <item>SEC1 (<c>BEGIN EC PRIVATE KEY</c>), plain.</item>
    /// <item>SEC1 encrypted the legacy way, with <c>Proc-Type: 4,ENCRYPTED</c> and a
    /// <c>DEK-Info</c> header. This is what <c>openssl ec -aes256</c> produces and the
    /// framework cannot read it on any target.</item>
    /// <item>PKCS#8 (<c>BEGIN PRIVATE KEY</c>), plain.</item>
    /// </list>
    /// Everything is parsed here rather than through <c>ImportFromPem</c> so the SDK keeps
    /// working on netstandard2.0, where those import methods do not exist.
    /// </remarks>
    internal static class PemKeyReader
    {
        private const string ProcTypeHeader = "Proc-Type:";
        private const string DekInfoHeader = "DEK-Info:";

        /// <summary>
        /// Reads an EC private key from PEM text.
        /// </summary>
        /// <param name="pem">The PEM text.</param>
        /// <param name="passphrase">The passphrase, if the key is encrypted.</param>
        /// <returns>An <see cref="ECDsa"/> holding the private key.</returns>
        public static ECDsa ReadEcPrivateKey(string pem, string passphrase)
        {
            if (string.IsNullOrWhiteSpace(pem))
            {
                throw new ArgumentException("The private key PEM is empty.", nameof(pem));
            }

            PemBlock block = PemBlock.Parse(pem);

            byte[] der = block.Body;
            if (block.IsEncrypted)
            {
                if (string.IsNullOrEmpty(passphrase))
                {
                    throw new CryptographicException(
                        "The private key is encrypted but no passphrase was supplied.");
                }

                der = DecryptLegacyPem(der, block.DekInfo, passphrase);
            }

            switch (block.Label)
            {
                case "EC PRIVATE KEY":
                    return FromSec1(der);
                case "PRIVATE KEY":
                    return FromPkcs8(der);
                case "ENCRYPTED PRIVATE KEY":
                    throw new CryptographicException(
                        "PKCS#8 encrypted private keys are not supported. Convert the key with "
                        + "`openssl ec -in key.pem -out key-sec1.pem -aes256`, or supply an "
                        + "unencrypted key.");
                default:
                    throw new CryptographicException(
                        $"Unsupported PEM block '{block.Label}'. An EC private key is required.");
            }
        }

        /// <summary>
        /// Builds an <see cref="ECDsa"/> from a SEC1 <c>ECPrivateKey</c> structure.
        /// </summary>
        /// <remarks>
        /// <code>
        /// ECPrivateKey ::= SEQUENCE {
        ///     version        INTEGER { ecPrivkeyVer1(1) },
        ///     privateKey     OCTET STRING,
        ///     parameters [0] ECParameters {{ NamedCurve }} OPTIONAL,
        ///     publicKey  [1] BIT STRING OPTIONAL }
        /// </code>
        /// </remarks>
        /// <param name="der">The DER bytes.</param>
        /// <returns>An <see cref="ECDsa"/> holding the private key.</returns>
        private static ECDsa FromSec1(byte[] der)
        {
            var reader = new DerReader(der);
            DerReader sequence = reader.ReadSequence();

            sequence.ReadInteger();
            byte[] privateKey = sequence.ReadOctetString();

            string curveOid = null;
            byte[] publicKey = null;
            while (sequence.HasData)
            {
                byte tag = sequence.PeekTag();
                if (tag == 0xA0)
                {
                    curveOid = sequence.ReadTagged(0xA0).ReadObjectIdentifier();
                }
                else if (tag == 0xA1)
                {
                    publicKey = sequence.ReadTagged(0xA1).ReadBitString();
                }
                else
                {
                    sequence.SkipValue();
                }
            }

            if (curveOid == null)
            {
                throw new CryptographicException(
                    "The EC private key does not name a curve. Only named curves are supported.");
            }

            return Create(curveOid, privateKey, publicKey);
        }

        /// <summary>
        /// Builds an <see cref="ECDsa"/> from a PKCS#8 <c>PrivateKeyInfo</c> structure.
        /// </summary>
        /// <param name="der">The DER bytes.</param>
        /// <returns>An <see cref="ECDsa"/> holding the private key.</returns>
        private static ECDsa FromPkcs8(byte[] der)
        {
            var reader = new DerReader(der);
            DerReader sequence = reader.ReadSequence();

            sequence.ReadInteger();

            DerReader algorithm = sequence.ReadSequence();
            string algorithmOid = algorithm.ReadObjectIdentifier();
            if (algorithmOid != "1.2.840.10045.2.1")
            {
                throw new CryptographicException(
                    $"Expected an EC key but the PKCS#8 algorithm is '{algorithmOid}'.");
            }

            string curveOid = algorithm.HasData ? algorithm.ReadObjectIdentifier() : null;
            byte[] inner = sequence.ReadOctetString();

            ECDsa key = FromSec1Inner(inner, curveOid);
            return key;
        }

        /// <summary>
        /// Reads the SEC1 structure nested inside a PKCS#8 wrapper, where the curve may
        /// instead be named by the outer algorithm identifier.
        /// </summary>
        /// <param name="der">The inner DER bytes.</param>
        /// <param name="curveOidFromWrapper">The curve OID from the PKCS#8 wrapper.</param>
        /// <returns>An <see cref="ECDsa"/> holding the private key.</returns>
        private static ECDsa FromSec1Inner(byte[] der, string curveOidFromWrapper)
        {
            var reader = new DerReader(der);
            DerReader sequence = reader.ReadSequence();

            sequence.ReadInteger();
            byte[] privateKey = sequence.ReadOctetString();

            string curveOid = curveOidFromWrapper;
            byte[] publicKey = null;
            while (sequence.HasData)
            {
                byte tag = sequence.PeekTag();
                if (tag == 0xA0)
                {
                    curveOid = sequence.ReadTagged(0xA0).ReadObjectIdentifier();
                }
                else if (tag == 0xA1)
                {
                    publicKey = sequence.ReadTagged(0xA1).ReadBitString();
                }
                else
                {
                    sequence.SkipValue();
                }
            }

            if (curveOid == null)
            {
                throw new CryptographicException("The EC private key does not name a curve.");
            }

            return Create(curveOid, privateKey, publicKey);
        }

        /// <summary>
        /// Assembles the <see cref="ECParameters"/> and creates the key.
        /// </summary>
        /// <param name="curveOid">The named curve OID.</param>
        /// <param name="privateKey">The private scalar.</param>
        /// <param name="publicKey">The uncompressed public point, if present.</param>
        /// <returns>An <see cref="ECDsa"/> holding the private key.</returns>
        private static ECDsa Create(string curveOid, byte[] privateKey, byte[] publicKey)
        {
            int fieldSize = FieldSizeForCurve(curveOid);

            var parameters = new ECParameters
            {
                Curve = ECCurve.CreateFromValue(curveOid),
                D = PadLeft(privateKey, fieldSize),
            };

            // The public point is optional in SEC1. When it is absent the framework can
            // derive it, but several platforms insist on being given Q, so prefer the
            // point from the file whenever it is there.
            if (publicKey != null && publicKey.Length == (2 * fieldSize) + 1 && publicKey[0] == 0x04)
            {
                var x = new byte[fieldSize];
                var y = new byte[fieldSize];
                Buffer.BlockCopy(publicKey, 1, x, 0, fieldSize);
                Buffer.BlockCopy(publicKey, 1 + fieldSize, y, 0, fieldSize);
                parameters.Q = new ECPoint { X = x, Y = y };
            }

            parameters.Validate();
            return ECDsa.Create(parameters);
        }

        /// <summary>
        /// Returns the coordinate size in bytes for a named curve.
        /// </summary>
        /// <param name="curveOid">The named curve OID.</param>
        /// <returns>The field size in bytes.</returns>
        private static int FieldSizeForCurve(string curveOid)
        {
            switch (curveOid)
            {
                case "1.2.840.10045.3.1.7": return 32; // secp256r1 / prime256v1
                case "1.3.132.0.34": return 48;        // secp384r1
                case "1.3.132.0.35": return 66;        // secp521r1
                case "1.3.132.0.10": return 32;        // secp256k1
                default:
                    throw new CryptographicException(
                        $"Unsupported EC curve '{curveOid}'. Upvest issues P-256, P-384 or P-521 keys.");
            }
        }

        /// <summary>
        /// Left-pads a big-endian integer to a fixed width, dropping any leading zero
        /// byte DER added to keep the value positive.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="length">The target width.</param>
        /// <returns>The padded value.</returns>
        private static byte[] PadLeft(byte[] value, int length)
        {
            if (value.Length == length)
            {
                return value;
            }

            if (value.Length > length)
            {
                int excess = value.Length - length;
                for (int i = 0; i < excess; i++)
                {
                    if (value[i] != 0)
                    {
                        throw new CryptographicException("The EC private scalar is too large for its curve.");
                    }
                }

                var trimmed = new byte[length];
                Buffer.BlockCopy(value, excess, trimmed, 0, length);
                return trimmed;
            }

            var padded = new byte[length];
            Buffer.BlockCopy(value, 0, padded, length - value.Length, value.Length);
            return padded;
        }

        /// <summary>
        /// Decrypts a legacy OpenSSL PEM body, the format announced by a
        /// <c>Proc-Type: 4,ENCRYPTED</c> header.
        /// </summary>
        /// <param name="body">The encrypted DER bytes.</param>
        /// <param name="dekInfo">The value of the DEK-Info header.</param>
        /// <param name="passphrase">The passphrase.</param>
        /// <returns>The decrypted DER bytes.</returns>
        private static byte[] DecryptLegacyPem(byte[] body, string dekInfo, string passphrase)
        {
            if (string.IsNullOrEmpty(dekInfo))
            {
                throw new CryptographicException(
                    "The private key is marked encrypted but carries no DEK-Info header.");
            }

            string[] parts = dekInfo.Split(',');
            if (parts.Length != 2)
            {
                throw new CryptographicException($"Malformed DEK-Info header '{dekInfo}'.");
            }

            string algorithm = parts[0].Trim().ToUpperInvariant();
            byte[] iv = FromHex(parts[1].Trim());

            int keySize;
            switch (algorithm)
            {
                case "AES-128-CBC": keySize = 16; break;
                case "AES-192-CBC": keySize = 24; break;
                case "AES-256-CBC": keySize = 32; break;
                default:
                    throw new CryptographicException(
                        $"Unsupported private key encryption '{algorithm}'. Re-encrypt the key with AES.");
            }

            // OpenSSL derives the key with EVP_BytesToKey(MD5), salted by the first eight
            // bytes of the IV.
            byte[] key = EvpBytesToKey(passphrase, iv, keySize);

            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = keySize * 8;
                aes.Key = key;
                aes.IV = iv;

                try
                {
                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        return decryptor.TransformFinalBlock(body, 0, body.Length);
                    }
                }
                catch (CryptographicException error)
                {
                    throw new CryptographicException(
                        "Could not decrypt the private key. The passphrase is probably wrong.", error);
                }
            }
        }

        /// <summary>
        /// OpenSSL's EVP_BytesToKey with MD5 and a single iteration.
        /// </summary>
        /// <param name="passphrase">The passphrase.</param>
        /// <param name="iv">The IV, whose first eight bytes are the salt.</param>
        /// <param name="keyLength">The number of key bytes required.</param>
        /// <returns>The derived key.</returns>
        private static byte[] EvpBytesToKey(string passphrase, byte[] iv, int keyLength)
        {
            byte[] password = Encoding.UTF8.GetBytes(passphrase);
            int saltLength = Math.Min(8, iv.Length);
            var salt = new byte[saltLength];
            Buffer.BlockCopy(iv, 0, salt, 0, saltLength);

            var key = new byte[keyLength];
            int generated = 0;
            byte[] previous = new byte[0];

            using (var md5 = MD5.Create())
            {
                while (generated < keyLength)
                {
                    var input = new byte[previous.Length + password.Length + salt.Length];
                    Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
                    Buffer.BlockCopy(password, 0, input, previous.Length, password.Length);
                    Buffer.BlockCopy(salt, 0, input, previous.Length + password.Length, salt.Length);

                    previous = md5.ComputeHash(input);
                    int take = Math.Min(previous.Length, keyLength - generated);
                    Buffer.BlockCopy(previous, 0, key, generated, take);
                    generated += take;
                }
            }

            return key;
        }

        /// <summary>
        /// Parses a hex string into bytes.
        /// </summary>
        /// <param name="hex">The hex string.</param>
        /// <returns>The bytes.</returns>
        private static byte[] FromHex(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                throw new CryptographicException($"Malformed hex value '{hex}'.");
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        /// <summary>
        /// One decoded PEM block along with its legacy encryption headers.
        /// </summary>
        private sealed class PemBlock
        {
            private PemBlock(string label, byte[] body, bool isEncrypted, string dekInfo)
            {
                Label = label;
                Body = body;
                IsEncrypted = isEncrypted;
                DekInfo = dekInfo;
            }

            public string Label { get; }

            public byte[] Body { get; }

            public bool IsEncrypted { get; }

            public string DekInfo { get; }

            public static PemBlock Parse(string pem)
            {
                string[] lines = pem.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

                string label = null;
                bool inBlock = false;
                bool inHeaders = false;
                bool isEncrypted = false;
                string dekInfo = null;
                var base64 = new StringBuilder();

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0)
                    {
                        // A blank line ends the legacy header section.
                        inHeaders = false;
                        continue;
                    }

                    if (line.StartsWith("-----BEGIN ", StringComparison.Ordinal))
                    {
                        label = line.Substring(11).Replace("-----", string.Empty).Trim();
                        inBlock = true;
                        inHeaders = true;
                        continue;
                    }

                    if (line.StartsWith("-----END ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (!inBlock)
                    {
                        continue;
                    }

                    if (inHeaders && line.StartsWith(ProcTypeHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        isEncrypted = line.IndexOf("ENCRYPTED", StringComparison.OrdinalIgnoreCase) >= 0;
                        continue;
                    }

                    if (inHeaders && line.StartsWith(DekInfoHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        dekInfo = line.Substring(DekInfoHeader.Length).Trim();
                        continue;
                    }

                    inHeaders = false;
                    base64.Append(line);
                }

                if (label == null || base64.Length == 0)
                {
                    throw new CryptographicException("No PEM block found in the private key.");
                }

                return new PemBlock(label, Convert.FromBase64String(base64.ToString()),
                    isEncrypted, dekInfo);
            }
        }

        /// <summary>
        /// A minimal DER reader, covering just the structures an EC private key file uses.
        /// </summary>
        private sealed class DerReader
        {
            private readonly byte[] buffer;
            private int position;
            private readonly int end;

            public DerReader(byte[] buffer)
                : this(buffer, 0, buffer.Length)
            {
            }

            private DerReader(byte[] buffer, int offset, int length)
            {
                this.buffer = buffer;
                this.position = offset;
                this.end = offset + length;
            }

            public bool HasData => position < end;

            public byte PeekTag()
            {
                EnsureAvailable(1);
                return buffer[position];
            }

            public DerReader ReadSequence() => ReadTagged(0x30);

            public DerReader ReadTagged(byte expectedTag)
            {
                byte tag = ReadTag();
                if (tag != expectedTag)
                {
                    throw new CryptographicException(
                        $"Malformed key: expected DER tag 0x{expectedTag:X2} but found 0x{tag:X2}.");
                }

                int length = ReadLength();
                var nested = new DerReader(buffer, position, length);
                position += length;
                return nested;
            }

            public byte[] ReadInteger()
            {
                ReadExpectedTag(0x02);
                int length = ReadLength();
                var value = new byte[length];
                Buffer.BlockCopy(buffer, position, value, 0, length);
                position += length;
                return value;
            }

            public byte[] ReadOctetString()
            {
                ReadExpectedTag(0x04);
                int length = ReadLength();
                var value = new byte[length];
                Buffer.BlockCopy(buffer, position, value, 0, length);
                position += length;
                return value;
            }

            public byte[] ReadBitString()
            {
                ReadExpectedTag(0x03);
                int length = ReadLength();
                if (length < 1)
                {
                    throw new CryptographicException("Malformed key: empty BIT STRING.");
                }

                // The first content byte counts unused trailing bits; keys never have any.
                int unusedBits = buffer[position];
                if (unusedBits != 0)
                {
                    throw new CryptographicException("Malformed key: BIT STRING is not byte-aligned.");
                }

                var value = new byte[length - 1];
                Buffer.BlockCopy(buffer, position + 1, value, 0, length - 1);
                position += length;
                return value;
            }

            public string ReadObjectIdentifier()
            {
                ReadExpectedTag(0x06);
                int length = ReadLength();
                EnsureAvailable(length);

                var parts = new List<string>();
                int offset = position;
                int limit = position + length;

                int first = buffer[offset++];
                parts.Add((first / 40).ToString(CultureInfo.InvariantCulture));
                parts.Add((first % 40).ToString(CultureInfo.InvariantCulture));

                long current = 0;
                while (offset < limit)
                {
                    byte octet = buffer[offset++];
                    current = (current << 7) | (long)(octet & 0x7F);
                    if ((octet & 0x80) == 0)
                    {
                        parts.Add(current.ToString(CultureInfo.InvariantCulture));
                        current = 0;
                    }
                }

                position = limit;
                return string.Join(".", parts.ToArray());
            }

            public void SkipValue()
            {
                ReadTag();
                int length = ReadLength();
                position += length;
            }

            private void ReadExpectedTag(byte expectedTag)
            {
                byte tag = ReadTag();
                if (tag != expectedTag)
                {
                    throw new CryptographicException(
                        $"Malformed key: expected DER tag 0x{expectedTag:X2} but found 0x{tag:X2}.");
                }
            }

            private byte ReadTag()
            {
                EnsureAvailable(1);
                return buffer[position++];
            }

            private int ReadLength()
            {
                EnsureAvailable(1);
                int first = buffer[position++];
                if ((first & 0x80) == 0)
                {
                    EnsureAvailable(first);
                    return first;
                }

                int byteCount = first & 0x7F;
                if (byteCount == 0 || byteCount > 4)
                {
                    throw new CryptographicException("Malformed key: unsupported DER length.");
                }

                EnsureAvailable(byteCount);
                int length = 0;
                for (int i = 0; i < byteCount; i++)
                {
                    length = (length << 8) | buffer[position++];
                }

                if (length < 0)
                {
                    throw new CryptographicException("Malformed key: negative DER length.");
                }

                EnsureAvailable(length);
                return length;
            }

            private void EnsureAvailable(int count)
            {
                if (position + count > end)
                {
                    throw new CryptographicException("Malformed key: truncated DER data.");
                }
            }
        }
    }
}

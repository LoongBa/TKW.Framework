using System;
using System.Security.Cryptography;

namespace TKW.Framework.Cryptography {
    public static class HashAlgorithmFactory
    {
        public static HashAlgorithm Create(HashAlgorithmType type)
        {
            return type switch
            {
                HashAlgorithmType.Md5 => MD5.Create(),
                HashAlgorithmType.Sha1 => SHA1.Create(),
                HashAlgorithmType.Sha256 => SHA256.Create(),
                HashAlgorithmType.Sha384 => SHA384.Create(),
                HashAlgorithmType.Sha512 => SHA512.Create(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
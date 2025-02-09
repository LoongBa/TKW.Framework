using System.Security.Cryptography;

namespace TKW.Framework.Cryptography
{
    public interface ISymmetricAlgorithmFactory
    {
        SymmetricAlgorithm Create(SymmetricAlgorithmType type);
    }
}

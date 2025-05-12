using System;
using System.Security.Cryptography; // For ProtectedData
using System.Text; // For Encoding

namespace QueryX.Services
{
    public class EncryptionService
    {
        // Optional: Define an entropy byte array for additional security.
        // If used, the same entropy must be used for both encryption and decryption.
        // For simplicity, we'll omit it here, which defaults to null entropy.
        // private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("YourAppSpecificEntropy");

        /// <summary>
        /// Encrypts a plain text string using DPAPI, scoped to the current user.
        /// </summary>
        /// <param name="plainText">The string to encrypt.</param>
        /// <returns>The encrypted data as a byte array, or null if input is null or empty.</returns>
        public byte[]? EncryptString(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return null;
            }

            try
            {
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                // Encrypt the data using DataProtectionScope.CurrentUser.
                // The optional entropy byte array can be added as a third argument to Protect method.
                return ProtectedData.Protect(plainTextBytes, null, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPAPI Encryption failed: {ex.Message}");
                // Handle or rethrow as appropriate for your application's error handling strategy
                throw; // Or return null, or a custom error object
            }
        }

        /// <summary>
        /// Decrypts data previously encrypted with DPAPI (CurrentUser scope).
        /// </summary>
        /// <param name="cipherTextBytes">The encrypted byte array.</param>
        /// <returns>The decrypted string, or null if input is null or decryption fails.</returns>
        public string? DecryptToString(byte[]? cipherTextBytes)
        {
            if (cipherTextBytes == null || cipherTextBytes.Length == 0)
            {
                return null;
            }

            try
            {
                // Decrypt the data using DataProtectionScope.CurrentUser.
                // The optional entropy byte array must match the one used for encryption if any.
                byte[] decryptedBytes = ProtectedData.Unprotect(cipherTextBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPAPI Decryption failed: {ex.Message}");
                // Handle or rethrow. Common issue: trying to decrypt on a different machine or as a different user.
                // Returning null here indicates decryption failure.
                return null;
            }
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace IGSB
{
    public static class EncryptionHelper
    {
        private static byte[] Salt { get => new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }; }

        public static string Encrypt(string clearText, string password)
        {
            try { 
                byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
                using (var encryptor = AesManaged.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(password, Salt);
                    encryptor.BlockSize = encryptor.LegalBlockSizes[0].MaxSize;
                    encryptor.KeySize = encryptor.LegalKeySizes[0].MaxSize;
                    encryptor.Key = pdb.GetBytes(encryptor.KeySize / 8);
                    encryptor.IV = pdb.GetBytes(encryptor.BlockSize / 8);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(clearBytes, 0, clearBytes.Length);
                        }
                        clearText = Convert.ToBase64String(ms.ToArray());
                    }
                } 
            } catch (Exception ex) { }
            return clearText;
        }
        public static string Decrypt(string cipherText, string password)
        {
            try
            {
                cipherText = cipherText.Replace(" ", "+");
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (var encryptor = AesManaged.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(password, Salt);
                    encryptor.BlockSize = encryptor.LegalBlockSizes[0].MaxSize;
                    encryptor.KeySize = encryptor.LegalKeySizes[0].MaxSize;
                    encryptor.Key = pdb.GetBytes(encryptor.KeySize / 8);
                    encryptor.IV = pdb.GetBytes(encryptor.BlockSize / 8);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                        }
                        cipherText = Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
            }
            catch (Exception ex) { }
            return cipherText;
        }
    }
}

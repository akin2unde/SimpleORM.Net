using System.Security.Cryptography;
using System.Text;

namespace SimpleORM.Net.AspNetCore
{
    internal static class Secure
    {
        // private static byte[] iv ={24,111,210,133,238,76,17,244,99
        // ,233,235,130,199,114,248,186};

        // private static byte[] key ={234,74,172,105,211,153,30,27,120,148,
        //     134,149,187,149,199,33,146,118,13,234,21,148,133,73,
        //     78,172,61,148,144,188,24,111};

        private static string ivString = "HW$1pIjHR$2pKj13";
        private static string keyString = "vbnNG001HWKE#@_5";

        private static Aes? rm;

        public static string Encode(string input)
        {
            byte[] key = Encoding.UTF8.GetBytes(keyString);
            byte[] iv = Encoding.UTF8.GetBytes(ivString);
            rm = Aes.Create();
            rm.IV = iv; ;
            rm.Key = key;

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                ICryptoTransform encryptor = rm.CreateEncryptor(rm.Key, rm.IV);
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(input);

                    }
                }
                return Convert.ToBase64String(msEncrypt.ToArray());
            }
        }

        public static string Decode(string input)
        {
            byte[] key = Encoding.UTF8.GetBytes(keyString);
            byte[] iv = Encoding.UTF8.GetBytes(ivString);
            rm = Aes.Create();
            rm.IV = iv; ;
            rm.Key = key;

            string? plaintext = null;

            using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(input)))
            {
                ICryptoTransform decryptor = rm.CreateDecryptor(rm.Key, rm.IV);
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }
            return plaintext;
        }

        // public static string GetHashedValue(string original)
        // {
        //     byte[] hash = MD5.Create().ComputeHash(new UnicodeEncoding().GetBytes(original));
        //     return Convert.ToBase64String(hash);
        // }
        public static string GetHashedValue(string original)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(original));
            var sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString().ToLower();
        }

    }
}
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;

public class AES256CBC
{
    private static string TRANSFORMATION = "AES/CBC/PKCS7PADDING";
    private byte[] AES_ENCRYPTION_KEY;
    private byte[] ENCRYPTION_IV;

    public AES256CBC(string privateKeyHex, string ephemPublicKeyHex, string encryptionIvHex)
    {
        using (SHA512 shaM = new SHA512Managed())
        {
            var e = ecdh(privateKeyHex, ephemPublicKeyHex);
            var b = toByteArray(e);

            byte[] hash = shaM.ComputeHash(b);
            byte[] encKeyBytes = new byte[32];

            System.Array.Copy(hash, encKeyBytes, 32);

            AES_ENCRYPTION_KEY = encKeyBytes;
            ENCRYPTION_IV = toByteArray(encryptionIvHex);

        }
    }

    public string encrypt(byte[] src)
    {
        var key = ParameterUtilities.CreateKeyParameter("AES", AES_ENCRYPTION_KEY);
        var parametersWithIv = new ParametersWithIV(key, ENCRYPTION_IV);

        var cipher = CipherUtilities.GetCipher(TRANSFORMATION);
        cipher.Init(true, parametersWithIv);

        return System.Text.Encoding.UTF8.GetString(
            cipher.DoFinal(src)
        );
    }

    public string decrypt(byte[] src)
    {
        var key = ParameterUtilities.CreateKeyParameter("AES", AES_ENCRYPTION_KEY);
        var parametersWithIv = new ParametersWithIV(key, ENCRYPTION_IV);

        var cipher = CipherUtilities.GetCipher(TRANSFORMATION);
        cipher.Init(false, parametersWithIv);

        return System.Text.Encoding.UTF8.GetString(
            cipher.DoFinal(src)
        );
    }


    public BigInteger ecdh(string privateKeyHex, string ephemPublicKeyHex)
    {
        string affineX = ephemPublicKeyHex.Substring(2, 66 - 2);
        string affineY = ephemPublicKeyHex.Substring(66);

        ECPointArithmetic ecPoint = new ECPointArithmetic(new FpCurve(
            new BigInteger("115792089237316195423570985008687907853269984665640564039457584007908834671663"),
            new BigInteger("0"),
            new BigInteger("7")), new BigInteger(affineX, 16), new BigInteger(affineY, 16), null);

        return ecPoint.multiply(new BigInteger(privateKeyHex, 16)).getX();
    }

    public static byte[] toByteArray(string s)
    {
        int len = s.Length;
        byte[] data = new byte[len / 2];
        for (int i = 0; i < len; i += 2)
        {
            data[i / 2] = (byte)(
                (System.Convert.ToInt32(s[i].ToString(), 16) << 4) +
                System.Convert.ToInt32(s[i + 1].ToString(), 16)
            );
        }
        return data;
    }

    public static byte[] toByteArray(BigInteger bi)
    {
        byte[] b = bi.ToByteArray();
        if (b.Length > 1 && b[0] == 0)
        {
            int n = b.Length - 1;
            byte[] newArray = new byte[n];
            System.Array.Copy(b, 1, newArray, 0, n);
            b = newArray;
        }
        return b;
    }
}

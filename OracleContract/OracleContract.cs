using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;

namespace OracleContract
{
    public class OracleContract : SmartContract
    {
        //管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("Aeto8Loxsh7nXWoVS4FzBkUrFuiCB3Qidn");

        private const string CONFIG_NEO_PRICE = "neo_price";
        private const string CONFIG_SDT_PRICE = "sdt_price";
          
        public static Object Main(string operation, params object[] args)
        { 
            var magicstr = "2018-07-02 15:16";
             
            if (operation == "setPrice")
            {
                if (args.Length != 2) return false;

                string key = (string)args[0];

                BigInteger price = (BigInteger)args[1];

               return setPrice(key, price);
            }

            if (operation == "getPrice")
            {
                if (args.Length != 2) return false;

                string key = (string)args[0];

                BigInteger price = (BigInteger)args[1];

                return getPrice(key);
            }
            
            return true;
        }

        public static bool setPrice(string key, BigInteger price)
        {
            if (key == null || key == "") return false;

            if (!Runtime.CheckWitness(admin)) return false;
            
            Storage.Put(Storage.CurrentContext, key, price);
            return true;
        }

        public static BigInteger getPrice(string key)
        { 
            BigInteger price = Storage.Get(Storage.CurrentContext, key).AsBigInteger();

            return price;
        }

    }
}


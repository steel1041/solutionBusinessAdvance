using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace OracleContract
{
    public class OracleContract : SmartContract
    {
        //管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("Aeto8Loxsh7nXWoVS4FzBkUrFuiCB3Qidn");

        private const string CONFIG_NEO_PRICE = "neo_price";
        private const string CONFIG_SDT_PRICE = "sdt_price";
        private const string CONFIG_ACCOUNT = "account_key";


        public static Object Main(string operation, params object[] args)
        {
            var callscript = ExecutionEngine.CallingScriptHash;

            var magicstr = "2018-07-02 15:16";

            if (operation == "setAccount")
            {
                if (args.Length != 1) return false;

                if (!Runtime.CheckWitness(admin)) return false;

                byte[] account = (byte[])args[0];

                Storage.Put(Storage.CurrentContext,CONFIG_ACCOUNT,account);

                return true;
            }
             
            if (operation == "setPrice")
            {
                if (args.Length != 3) return false;

                string key = (string)args[0];

                byte[] from = (byte[])args[1];

                byte[] account = Storage.Get(Storage.CurrentContext, CONFIG_ACCOUNT);
                 
                BigInteger price = (BigInteger)args[2];
                 

                if (callscript.AsBigInteger() != from.AsBigInteger() || (!Runtime.CheckWitness(from) || from != account)) return false;

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


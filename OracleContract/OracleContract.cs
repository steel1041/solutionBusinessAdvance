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
        private const string CONFIG_GAS_PRICE = "gas_price";
        private const string CONFIG_SDT_PRICE = "sdt_price";
        private const string CONFIG_ACCOUNT = "account_key";

        //C端参数配置
        private const string CONFIG_LIQUIDATION_RATE_C = "liqu_rate_c";
        private const string CONFIG_WARNING_RATE_C = "warning_rate_c";
         
        //B端参数配置
        private const string CONFIG_LIQUIDATION_RATE_B = "liqu_rate_b";
        private const string CONFIG_WARNING_RATE_B = "warning_rate_b";


        public static Object Main(string operation, params object[] args)
        {
            var callscript = ExecutionEngine.CallingScriptHash;

            var magicstr = "2018-07-04 15:16";

            if (operation == "setAccount")
            {
                if (args.Length != 1) return false;

                if (!Runtime.CheckWitness(admin)) return false;

                byte[] account = (byte[])args[0];

                Storage.Put(Storage.CurrentContext,CONFIG_ACCOUNT,account);

                return true;
            }

            if (operation == "getAccount")
            {
                return Storage.Get(Storage.CurrentContext,CONFIG_ACCOUNT);
            }

            if (operation == "setConfig")
            {
                if (args.Length != 2) return false;

                if (!Runtime.CheckWitness(admin)) return false;

                string key = (string)args[0];

                BigInteger value = (BigInteger)args[1];

                return setConfig(key, value);
            }

            if (operation == "getConfig")
            { 
                if (args.Length != 1) return false;

                string key = (string)args[0];
                  
                return getConfig(key);
            }
             
            if (operation == "setPrice")
            {
                if (args.Length != 3) return false;

                string key = (string)args[0];

                byte[] from = (byte[])args[1];

                byte[] account = Storage.Get(Storage.CurrentContext, CONFIG_ACCOUNT);
                 
                BigInteger price = (BigInteger)args[2];



                //允许合约或者授权账户调用
                if (callscript.AsBigInteger() != from.AsBigInteger() && (!Runtime.CheckWitness(from) || from != account)) return false;
                  
                  return setPrice(key, price);
            }
             
            if (operation == "getPrice")
            {
                if (args.Length != 1) return false;

                string key = (string)args[0];
                  
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

        public static bool setConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;

            Storage.Put(Storage.CurrentContext, key, value); 
            return true;
        }

        public static BigInteger getConfig(string key)
        {
            BigInteger value = Storage.Get(Storage.CurrentContext, key).AsBigInteger();

            return value;
        }

    }
}


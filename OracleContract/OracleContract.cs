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
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        private const string CONFIG_NEO_PRICE = "neo_price";
        private const string CONFIG_GAS_PRICE = "gas_price";
        private const string CONFIG_SDS_PRICE = "sds_price";
        private const string CONFIG_ACCOUNT = "account_key";

        //C端参数配置
        private const string CONFIG_LIQUIDATION_RATE_C = "liquidate_rate_c";
        private const string CONFIG_WARNING_RATE_C = "warning_rate_c";

        //B端参数配置
        private const string CONFIG_LIQUIDATION_RATE_B = "liquidate_rate_b";
        private const string CONFIG_WARNING_RATE_B = "warning_rate_b";
          
        public static Object Main(string operation, params object[] args)
        {
            var callscript = ExecutionEngine.CallingScriptHash;

            var magicstr = "2018-08-14 15:16";

            //为账户做授权操作
            if (operation == "setAccount")
            {
                if (args.Length != 2) return false;

                if (!Runtime.CheckWitness(admin)) return false;

                byte[] account = (byte[])args[0];

                if (account.Length != 20) return false;

                //设置授权状态,state = 0未授权,state != 0 授权
                BigInteger state = (BigInteger)args[1];


                Storage.Put(Storage.CurrentContext, new byte[] {0x01 }.Concat(account), state);

                return true;
            }

            //获取账户授权状态
            if (operation == "getAccount")
            {
                if (args.Length != 1) return false;

                byte[] account = (byte[])args[0];

                if (account.Length != 20) return false;

                return Storage.Get(Storage.CurrentContext, new byte[] { 0x01 }.Concat(account));
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
            /* 设置代币价格  
            *  neo_price    50*100000000
            *  gas_price    20*100000000  
            *  sds_price    0.08*100000000
            *  
            */
            if (operation == "setPrice")
            {
                if (args.Length != 3) return false;

                string key = (string)args[0];
                
                byte[] from = (byte[])args[1];
                 
                BigInteger state = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { 0x01 }.Concat(from)).AsBigInteger();

                BigInteger price = (BigInteger)args[2];


                //允许合约或者授权账户调用
                if (callscript.AsBigInteger() != from.AsBigInteger() && (!Runtime.CheckWitness(from) || state == 0)) return false;

                return setPrice(key, price);
            }
            if (operation == "getPrice")
            {
                if (args.Length != 1) return false;
                string key = (string)args[0];

                return getPrice(key);
            }


            //设置锚定物对应100000000美元汇率
            /*  
             *  anchor_type_usd    1*100000000
             *  anchor_type_cny    6.8*100000000
             *  anchor_type_eur    0.875*100000000
             *  anchor_type_jpy    120*100000000
             *  anchor_type_gbp    0.7813 *100000000
             *  anchor_type_gold   0.000838 * 100000000
             */
            if (operation == "setAnchorPrice")
            {
                if (args.Length != 2) return false;

                string key = (string)args[0];

                if (!Runtime.CheckWitness(admin)) return false;

                BigInteger price = (BigInteger)args[1];

                return setAnchorPrice(key, price);
            }

            //获取锚定物对应美元汇率
            if (operation == "getAnchorPrice")
            {
                if (args.Length != 1) return false;

                string key = (string)args[0];

                return getAnchorPrice(key);
            }

            return true;
        }


        public static bool setAnchorPrice(string key, BigInteger price)
        {
            if (key == null || key == "") return false;

            Storage.Put(Storage.CurrentContext, key, price);
            return true;
        }

        public static bool setPrice(string key, BigInteger price)
        {
            if (key == null || key == "") return false;

            if (price < 0) return false;

            Storage.Put(Storage.CurrentContext, key, price);
            return true;
        }

        public static BigInteger getPrice(string key)
        {
            BigInteger price = Storage.Get(Storage.CurrentContext, key).AsBigInteger();

            return price;
        }

        public static BigInteger getAnchorPrice(string key)
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


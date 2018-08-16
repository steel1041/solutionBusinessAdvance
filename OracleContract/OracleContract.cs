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

        //initToken 手续费
        private const string SERVICE_FEE = "service_fee";
         
        public static Object Main(string operation, params object[] args)
        { 
            var callscript = ExecutionEngine.CallingScriptHash;
             
           byte[] ref ACCOUNT_KEY = new byte[] { 0x01 };
             
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


                Storage.Put(Storage.CurrentContext, ACCOUNT_KEY.Concat(account), state);

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

                var dic = new byte[] { 0x01 };
                BigInteger state = (BigInteger)Storage.Get(Storage.CurrentContext, dic.Concat(from)).AsBigInteger();

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
            #region 升级合约,耗费490,仅限管理员
            if (operation == "upgrade")
            {
                //不是管理员 不能操作
                if (!Runtime.CheckWitness(admin))
                    return false;

                if (args.Length != 1 && args.Length != 9)
                    return false;

                byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                byte[] new_script = (byte[])args[0];
                //如果传入的脚本一样 不继续操作
                if (script == new_script)
                    return false;

                byte[] parameter_list = new byte[] { 0x07, 0x10 };
                byte return_type = 0x05;
                //1|0|4
                bool need_storage = (bool)(object)05;
                string name = "business";
                string version = "1";
                string author = "alchemint";
                string email = "0";
                string description = "alchemint";

                if (args.Length == 9)
                {
                    parameter_list = (byte[])args[1];
                    return_type = (byte)args[2];
                    need_storage = (bool)args[3];
                    name = (string)args[4];
                    version = (string)args[5];
                    author = (string)args[6];
                    email = (string)args[7];
                    description = (string)args[8];
                }
                Contract.Migrate(new_script, parameter_list, return_type, need_storage, name, version, author, email, description);
                return true;
            }
            #endregion

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


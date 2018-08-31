using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace DataCenterContract2

{
    public class DataCenterContract2 : SmartContract
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

        //NEO价格key,index为16进制数字0x01、0x02
        private static byte[] getNeoPriceKey(byte[] index) => "neo_price".AsByteArray().Concat(index);

        private static byte[] getConfigKey(byte[] key) => new byte[] { 0x03 }.Concat(key);

        public static Object Main(string operation, params object[] args)
        {
            var callscript = ExecutionEngine.CallingScriptHash;

            var magicstr = "2018-08-29 15:16";

            //为账户做授权操作
            if (operation == "setAccount")
            {
                if (args.Length != 2) return false;

                if (!Runtime.CheckWitness(admin)) return false;

                byte[] account = (byte[])args[0];

                if (account.Length != 20) return false;

                //设置授权状态,state = 0未授权,state != 0 授权
                BigInteger state = (BigInteger)args[1];

                Storage.Put(Storage.CurrentContext, new byte[] { 0x01 }.Concat(account), state);

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

            /*设置全局参数
             * liquidate_rate_b 150
             * warning_rate_c 120
             */
            /*设置锚定物白名单
             *anchor_type_gold   1:黑名单 0:白名单
             */
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
            */

            //设置锚定物对应100000000美元汇率
            /*  
             *  anchor_type_usd    1*100000000
             *  anchor_type_cny    6.8*100000000
             *  anchor_type_eur    0.875*100000000
             *  anchor_type_jpy    120*100000000
             *  anchor_type_gbp    0.7813 *100000000
             *  anchor_type_gold   0.000838 * 100000000
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

            if (operation == "setNeoPrice")
            {
                if (args.Length != 2) return false;
                BigInteger index = (BigInteger)args[0];
                BigInteger price = (BigInteger)args[1];

                byte[] key = getNeoPriceKey(index.AsByteArray());
                return setNeoPrice(key, price);
            }

            if (operation == "getNeoPrice")
            {
                if (args.Length != 1) return false;
                BigInteger index = (BigInteger)args[0];
                return getNeoPrice(getNeoPriceKey(index.AsByteArray()));
            }

            if (operation == "getPrice")
            {
                if (args.Length != 1) return false;
                string key = (string)args[0];

                return getPrice(key);
            }

            if (operation == "setMedian")
            {
                if (args.Length != 1) return false;
                int index = (int)args[0];
                return getMedian(index);
            }

            if (operation == "setPow")
            {
                if (args.Length != 2) return false;
                int x = (int)args[0];
                int y = (int)args[1];
                int z =  mypow(x,y);

                Storage.Put(Storage.CurrentContext, getConfigKey("pow".AsByteArray()),z);
                return true;
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

        public static bool setPrice(string key, BigInteger price)
        {
            if (key == null || key == "") return false;

            if (price < 0) return false;

            byte[] byteKey = new byte[] { 0x02 }.Concat(key.AsByteArray());

            Storage.Put(Storage.CurrentContext, byteKey, price);
            return true;
        }

        public static bool setNeoPrice(byte[] key, BigInteger price)
        {

            Storage.Put(Storage.CurrentContext, key, price);
            return true;
        }

        public static BigInteger getPrice(string key)
        {
            byte[] byteKey = new byte[] { 0x02 }.Concat(key.AsByteArray());

            BigInteger price = Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger();

            return price;
        }

        public static BigInteger getNeoPrice(byte[] key)
        {

            BigInteger price = Storage.Get(Storage.CurrentContext, key).AsBigInteger();

            return price;
        }

        public static bool setConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;

            byte[] byteKey = getConfigKey(key.AsByteArray());

            Storage.Put(Storage.CurrentContext, byteKey, value);
            return true;
        }

        public static BigInteger getConfig(string key)
        {

            byte[] byteKey = getConfigKey(key.AsByteArray());

            BigInteger value = Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger();

            return value;
        }

        private static bool getMedian(int n)
        {

            //double[] arr = new double[] { 1, 1.1, 2.3, 4.5, 7, 8 };
            //为了不修改arr值，对数组的计算和修改在tempArr数组中进行

            //BigInteger[] arr
            BigInteger[] tempArr = new BigInteger[n];

            for (int k=0;k<n;k++)
            {
                BigInteger l = k + 1;
                //byte[] param = new byte[] { l };
                byte[] key = getNeoPriceKey(l.AsByteArray());
                tempArr[k] = Storage.Get(Storage.CurrentContext,key).AsBigInteger();
            }
            //tempArr[0] = Storage.Get(Storage.CurrentContext, "neo_price_01").AsBigInteger();
            //tempArr[1] = Storage.Get(Storage.CurrentContext, "neo_price_02").AsBigInteger();
            //tempArr[2] = Storage.Get(Storage.CurrentContext, "neo_price_03").AsBigInteger();
            //tempArr[3] = Storage.Get(Storage.CurrentContext, "neo_price_04").AsBigInteger();
            //tempArr[4] = Storage.Get(Storage.CurrentContext, "neo_price_05").AsBigInteger();


            //BigInteger[] tempArr = new BigInteger[5];

            //for (int k=0;k<arr.Length;k++) {
            //    tempArr[k] = arr[k];
            //}

            //对数组进行排序
            BigInteger temp;
            for (int i = 0; i < tempArr.Length; i++)
            {
                for (int j = i; j < tempArr.Length; j++)
                {
                    if (tempArr[i] > tempArr[j])
                    {
                        temp = tempArr[i];
                        tempArr[i] = tempArr[j];
                        tempArr[j] = temp;
                    }
                }
            }

            BigInteger result = 0;
            //针对数组元素的奇偶分类讨论
            if (tempArr.Length % 2 != 0)
            {
                result =  tempArr[tempArr.Length / 2 + 1];
            }
            else
            {
                result = (tempArr[tempArr.Length / 2] + tempArr[tempArr.Length / 2 + 1]) / 2;
            }

            Storage.Put(Storage.CurrentContext, getConfigKey("result".AsByteArray()),result);
            return true;
        }

        private static int mypow(int x, int y)
        {
            if (y < 0)
            {
                return 0;
            }
            if (y == 0)
            {
                return 1;
            }
            if (y == 1)
            {
                return x;
            }
            int result = x;
            for (int i = 1; i < y; i++)
            {
                result *= x;
            }
            return result;
        }

    }
}


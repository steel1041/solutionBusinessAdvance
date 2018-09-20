using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace OracleCOntract2
{
    public class Contract1 : SmartContract
    {
        //管理员账户 
        private static readonly byte[] admin = Helper.ToScriptHash("AQdP56hHfo54JCWfpPw4MXviJDtQJMtXFa");

        private const string CONFIG_NEO_PRICE = "neo_price";
        private const string CONFIG_GAS_PRICE = "gas_price";
        private const string CONFIG_SDS_PRICE = "sds_price";
        private const string CONFIG_ACCOUNT = "account_key";
        private const string CONFIG_ADDRESS_COUNT = "address_count_key";


        private static byte[] GetTypeAParaKey(byte[] account) => new byte[] { 0x01 }.Concat(account); 
        private static byte[] GetTypeAKey(string strKey) => new byte[] { 0x02 }.Concat(strKey.AsByteArray());
        private static byte[] GetTypeBKey(string key, BigInteger index) => new byte[] { 0x03 }.Concat(key.AsByteArray().Concat(index.AsByteArray()));

        private static byte[] GetParaAddrKey(string paraKey, byte[] addr) => new byte[] { 0x10 }.Concat(paraKey.AsByteArray().Concat(addr));
        private static byte[] GetParaCountKey(string paraKey) => new byte[] { 0x11 }.Concat(paraKey.AsByteArray());
        private static byte[] GetAddrIndexKey(string paraKey,byte[] addr) => new byte[] { 0x13 }.Concat(paraKey.AsByteArray().Concat(addr));
        private static byte[] GetMedianKey(string key) => new byte[] { 0x20 }.Concat(key.AsByteArray());
        private static byte[] GetAverageKey(string key) => new byte[] { 0x21 }.Concat(key.AsByteArray());


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

            var magicstr = "2018-08-29 15:16";
            
            //管理员添加TypeA的合法参数
            if (operation == "addTypeAParaWhit")
            {
                if (args.Length != 2) return false;

                if (!Runtime.CheckWitness(admin)) return false;

                byte[] account = (byte[])args[0];

                if (account.Length != 20) return false;

                //设置授权状态,state = 0未授权,state != 0 授权
                BigInteger state = (BigInteger)args[1];

                byte[] byteKey = GetTypeAParaKey(account);

                Storage.Put(Storage.CurrentContext, byteKey, state);

                return true;
            }

            //管理员移除TypeA的合法参数
            if (operation == "removeTypeAParaWhit")
            {
                if (args.Length != 1) return false;

                byte[] addr = (byte[])args[0];

                if (!Runtime.CheckWitness(admin)) return false;

                byte[] byteKey = GetTypeAParaKey(addr);

                Storage.Delete(Storage.CurrentContext, byteKey);

                return true;
            }

            /*设置全局参数
            * liquidate_rate_b 150
            * warning_rate_c 120
            */
            /*设置锚定物白名单
             *anchor_type_gold   1:黑名单 0:白名单
             */
            if (operation == "setTypeA")
            {
                if (args.Length != 2) return false;

                string key = (string)args[0];

                BigInteger value = (BigInteger)args[1];

                return setTypeA(key, value);
            }

            if (operation == "getTypeA")
            {
                if (args.Length != 1) return false;

                string key = (string)args[0];

                return getTypeA(key);
            }

            //管理员添加某个参数合法外部喂价器地址
            if (operation == "addParaAddrWhit")
            {
                if (args.Length != 3) return false;

                string para = (string)args[0];

                byte[] addr = (byte[])args[1];

                BigInteger state = (BigInteger)args[2]; //设置授权状态state != 0 授权

                return addParaAddrWhit(para, addr, state);
            }

            //管理员移除某个参数合法外部喂价器地址
            if (operation == "removeParaAddrWhit")
            {
                if (args.Length != 2) return false;

                string para = (string)args[0];

                byte[] addr = (byte[])args[1];

                return removeParaAddrWhit(para, addr);
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

            if (operation == "setTypeB")
            {
                if (args.Length != 3) return false;

                string para = (string)args[0];
                
                byte[] from = (byte[])args[1];

                BigInteger value = (BigInteger)args[2];
                 
                BigInteger state = (BigInteger)Storage.Get(Storage.CurrentContext, GetParaAddrKey(para,from)).AsBigInteger();
                
                //允许合约或者授权账户调用
                if (callscript.AsBigInteger() != from.AsBigInteger() && state == 0) return false;

                return setTypeB(para, from, value);
            }

            if (operation == "getTypeB")
            {
                if (args.Length != 1) return false;
                string key = (string)args[0];

                return getTypeB(key);
            }

            if (operation == "getAverage")
            {
                if (args.Length != 1) return false;
                string key = (string)args[0];

                return getAverage(key);
            }

            if (operation == "getMedian")
            {
                if (args.Length != 1) return false;
                string key = (string)args[0];

                return getMedian(key);
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
                string name = "datacenter";
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

        public static bool addParaAddrWhit(string para, byte[] addr, BigInteger state)
        {
           if (!Runtime.CheckWitness(admin)) return false;

           if (addr.Length != 20) return false;

           byte[] byteKey = GetParaAddrKey(para, addr);

           if (Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger() != 0 || state == 0) return false;

           Storage.Put(Storage.CurrentContext, byteKey, state);

           byte[] paraCountByteKey = GetParaCountKey(para);

           BigInteger paraCount = Storage.Get(Storage.CurrentContext, paraCountByteKey).AsBigInteger();
            
           paraCount += 1; 

           Storage.Put(Storage.CurrentContext, GetAddrIndexKey(para,addr), paraCount);
           
           Storage.Put(Storage.CurrentContext, paraCountByteKey, paraCount);
            
           return true;
        }

        public static bool removeParaAddrWhit(string para, byte[] addr)
        {
            if (!Runtime.CheckWitness(admin)) return false;

            byte[] paraAddrByteKey = GetParaAddrKey(para, addr);

            Storage.Delete(Storage.CurrentContext, paraAddrByteKey);

            byte[] paraCountByteKey = GetParaCountKey(para);

            Storage.Put(Storage.CurrentContext, GetAddrIndexKey(para, addr), 0); 
            
            return true;
        }
        
        public static bool setTypeA(string key, BigInteger value)
        {
            if (key == null || key == "") return false;

            if (!Runtime.CheckWitness(admin)) return false;

            byte[] byteKey = GetTypeAKey(key);

            Storage.Put(Storage.CurrentContext, byteKey, value);

            return true;
        }

        public static BigInteger getTypeA(string key)
        {

            byte[] byteKey = GetTypeAKey(key);

            BigInteger value = Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger();

            return value;
        }

        public static bool setTypeB(string key,byte[] addr, BigInteger value)
        {
            if (key == null || key == "") return false;

            if (value < 0) return false;

            if (!Runtime.CheckWitness(addr)) return false;

            BigInteger index = Storage.Get(Storage.CurrentContext, GetAddrIndexKey(key,addr)).AsBigInteger();
             
            Storage.Put(Storage.CurrentContext, GetTypeBKey(key,index), value);

            getAverage(key);

            getMedian(key);

            return true;
        }

        public static BigInteger getTypeB(string key)
        {
            return computeMedian(key);
        }

        public static BigInteger getAverage(string key)
        {
            return Storage.Get(Storage.CurrentContext, GetAverageKey(key)).AsBigInteger();
        }

        public static BigInteger getMedian(string key)
        {
            return Storage.Get(Storage.CurrentContext, GetMedianKey(key)).AsBigInteger();
        }

        public static BigInteger computeAverage(string key)
        { 
            BigInteger paraCount = Storage.Get(Storage.CurrentContext, GetParaCountKey(key)).AsBigInteger();

            var prices = new BigInteger[(int)paraCount];

            for (int i = 0; i < prices.Length; i++)
            {
                BigInteger keyIndex = i + 1;

                byte[] byteKey = GetTypeBKey(key, keyIndex);
                BigInteger val = Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger();

                if (val != 0)
                {
                    prices[i] = val;
                }
            }

            BigInteger sum = 0;
            for (int i = 0; i < prices.Length; i++)
            {
                sum = sum + prices[i];
            }

            BigInteger value = sum / prices.Length;
             
            Storage.Put(Storage.CurrentContext, GetAverageKey(key), value);
             
            return value; 
        }

        public static BigInteger computeMedian(string key)
        {
            BigInteger paraCount = Storage.Get(Storage.CurrentContext, GetParaCountKey(key)).AsBigInteger();

            var prices = new BigInteger[(int)paraCount];

            for (int i = 0; i < prices.Length; i++)
            {
                BigInteger keyIndex = i + 1;

                byte[] byteKey = GetTypeBKey(key,keyIndex);
                BigInteger val = Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger();

                if (val != 0)
                {
                    prices[i] = val;
                } 
            }

            BigInteger temp;
            for (int i = 0; i < prices.Length; i++)
            {
                for (int j = i; j < prices.Length; j++)
                {
                    if (prices[i] > prices[j])
                    {
                        temp = prices[i];
                        prices[i] = prices[j];
                        prices[j] = temp;
                    }
                }
            }

            BigInteger value = 0;
            if (prices.Length % 2 != 0)
            {
                value = prices[(prices.Length + 1) / 2 - 1];
            }
            else
            {
                int index = prices.Length / 2;

                value = (prices[index] + prices[index - 1]) / 2;
            }

            Storage.Put(Storage.CurrentContext, GetMedianKey(key), value);

            return value;
        }
    }
}

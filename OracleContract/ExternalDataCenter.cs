using Neo.SmartContract.Framework; 
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace OracleContract

{
    public class ExternalDataCenter : SmartContract
    {
        //管理员账户 
        private static readonly byte[] admin = Helper.ToScriptHash("AQdP56hHfo54JCWfpPw4MXviJDtQJMtXFa");

        private const string CONFIG_NEO_PRICE = "neo_price";
        private const string CONFIG_GAS_PRICE = "gas_price";
        private const string CONFIG_SDS_PRICE = "sds_price";
        private const string CONFIG_ACCOUNT = "account_key";
        private const string CONFIG_ADDRESS_COUNT = "address_count_key";
   
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

            if (operation == "test")
            {
                BigInteger index = (BigInteger)args[0];

                string key = (string)args[1];

                BigInteger keyIndex = (BigInteger)args[2];

                byte[] addr = (byte[])args[3];

                BigInteger value = (BigInteger)args[4];

                if (index == 1)
                { 
                    return Storage.Get(Storage.CurrentContext, CONFIG_ADDRESS_COUNT);
                }

                if (index == 2)
                { 
                    byte[] bytePrefix = new byte[] { 0x02 };

                    byte[] byteKey = key.AsByteArray().Concat(keyIndex.AsByteArray());

                    return Storage.Get(Storage.CurrentContext, bytePrefix.Concat(byteKey)).AsBigInteger(); 
                 }

                if (index == 3)
                {
                    return Storage.Get(Storage.CurrentContext, new byte[] { 0x10 }.Concat(addr));
                }

                if (index == 4)
                {
                    byte[] bytePrefix = new byte[] { 0x02 };

                    byte[] byteKey = key.AsByteArray().Concat(keyIndex.AsByteArray());

                    Storage.Put(Storage.CurrentContext, bytePrefix.Concat(byteKey),value);

                    return true;
                }

                return Storage.Get(Storage.CurrentContext,key);
            }

            //管理员添加TypeA的合法参数
            if (operation == "addTypeAParaWhit")
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

            //管理员移除TypeA的合法参数
            if (operation == "removeTypeAParaWhit")
            {
                if (args.Length != 1) return false;

                byte[] addr = (byte[])args[0];
                 
                if (!Runtime.CheckWitness(admin)) return false;

                Storage.Delete(Storage.CurrentContext, new byte[] { 0x01 }.Concat(addr));

                return true;
            }

            //管理员新增TypeB的合法参数
            if (operation == "addTypeBParaWhit")
            {
                if (args.Length != 2) return false;

                if (!Runtime.CheckWitness(admin)) return false;

                byte[] account = (byte[])args[0];

                if (account.Length != 20) return false;

                //设置授权状态state != 0 授权
                BigInteger state = (BigInteger)args[1];

                byte[] bytePrefix = new byte[] { 0x10 };

                if (Storage.Get(Storage.CurrentContext, bytePrefix.Concat(account)).AsBigInteger() != 0 || state == 0) return false;

                Storage.Put(Storage.CurrentContext, bytePrefix.Concat(account), state);
                
                BigInteger count = Storage.Get(Storage.CurrentContext, CONFIG_ADDRESS_COUNT).AsBigInteger();

                count += 1;

                Storage.Put(Storage.CurrentContext, CONFIG_ADDRESS_COUNT, count);
                 
                return true;
            }

            //管理员移除TypeB的合法参数
            if (operation == "removeTypeBParaWhit")
            {
                if (args.Length != 1) return false;

                byte[] addr = (byte[])args[0];

                if (!Runtime.CheckWitness(admin)) return false;

                Storage.Delete(Storage.CurrentContext, new byte[] { 0x10 }.Concat(addr));

                BigInteger count = Storage.Get(Storage.CurrentContext, CONFIG_ADDRESS_COUNT).AsBigInteger();

                count -= 1;

                if (count == 0)
                {
                    Storage.Delete(Storage.CurrentContext, CONFIG_ADDRESS_COUNT);
                }
                else {

                    Storage.Put(Storage.CurrentContext, CONFIG_ADDRESS_COUNT, count);
                }

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

                if (!Runtime.CheckWitness(admin)) return false;

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
                if (args.Length != 4) return false;

                string key = (string)args[0];
                
                BigInteger keyIndex = (BigInteger)args[1];

                byte[] from = (byte[])args[2];
                 
                BigInteger state = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { 0x10 }.Concat(from)).AsBigInteger();

                BigInteger value = (BigInteger)args[3];
                
                //允许合约或者授权账户调用
                if (callscript.AsBigInteger() != from.AsBigInteger() && (!Runtime.CheckWitness(from) || state == 0)) return false;

                return setTypeB(key, keyIndex, value);
            }
            if (operation == "getTypeB")
            {
                if (args.Length != 1) return false;
                string key = (string)args[0];

                return getTypeB(key);
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


        public static bool setTypeA(string key, BigInteger value)
        {
            if (key == null || key == "") return false;

            byte[] byteKey = new byte[] { 0x03 }.Concat(key.AsByteArray());

            Storage.Put(Storage.CurrentContext, byteKey, value);
            return true;
        }

        public static BigInteger getTypeA(string key)
        {

            byte[] byteKey = new byte[] { 0x03 }.Concat(key.AsByteArray());

            BigInteger value = Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger();

            return value;
        }

        public static bool setTypeB(string key,BigInteger keyIndex ,BigInteger value)
        {
            if (key == null || key == "") return false;

            if (value < 0) return false;

            BigInteger count = Storage.Get(Storage.CurrentContext, CONFIG_ADDRESS_COUNT).AsBigInteger();

            if (keyIndex > count || keyIndex == 0) return false;

            byte[] bytePrefix = new byte[] { 0x02 };

            byte[] byteKey = key.AsByteArray().Concat(keyIndex.AsByteArray());
             
            Storage.Put(Storage.CurrentContext, bytePrefix.Concat(byteKey), value);
            return true;
        }

        public static BigInteger getTypeB(string key)
        { 
            return computeTypeB(key);
        } 
           
        public static BigInteger computeTypeB(string key) {

            byte[] bytePrefix = new byte[] { 0x02 };

            BigInteger count = Storage.Get(Storage.CurrentContext, CONFIG_ADDRESS_COUNT).AsBigInteger();
             
            var prices = new BigInteger[(int)count];
             
            for (int i = 0; i < prices.Length; i++)
            {
                BigInteger keyIndex = i + 1;

                byte[] byteKey = key.AsByteArray().Concat(keyIndex.AsByteArray());

                prices[i] = Storage.Get(Storage.CurrentContext,bytePrefix.Concat(byteKey)).AsBigInteger(); 
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

            return value;
        } 
    }
}

